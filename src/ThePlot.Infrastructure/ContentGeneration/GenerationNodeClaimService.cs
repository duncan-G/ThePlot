using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using ThePlot.Core.ContentGeneration;

namespace ThePlot.Infrastructure.ContentGeneration;

internal sealed class GenerationNodeClaimService(
    ThePlotContext db,
    IOptions<ContentGenerationOptions> options) : IGenerationNodeClaimService
{
    public async Task<ClaimedGenerationWork?> TryClaimNextAsync(string workerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leaseEnd = now + options.Value.LeaseDuration;

        var phase = GenerationWorkflowPhase.ContentGeneration.ToString();
        var runRunning = GenerationRunStatus.Running.ToString();
        var depDone = GenerationNodeStatus.Succeeded.ToString();
        var nodeRunning = GenerationNodeStatus.Running.ToString();
        var setRunning = GenerationNodeStatus.Running.ToString();

        var statuses = string.Join(
            ", ",
            new[]
            {
                GenerationNodeStatus.Ready.ToString(),
                GenerationNodeStatus.Pending.ToString(),
                GenerationNodeStatus.NeedsRetry.ToString(),
            }.Select(s => $"'{s}'"));

        var sql = $"""
            WITH next AS (
              SELECT gn.id
              FROM theplot.generation_nodes AS gn
              INNER JOIN theplot.generation_runs AS gr ON gr.id = gn.generation_run_id
              WHERE gr.phase = @phase
                AND gr.status = @runStatus
                AND (
                  gn.status IN ({statuses})
                  OR (gn.status = @nodeRunning AND gn.lease_expires_at_utc IS NOT NULL AND gn.lease_expires_at_utc < @now)
                )
                AND (gn.runnable_after_utc IS NULL OR gn.runnable_after_utc <= @now)
                AND NOT EXISTS (
                  SELECT 1
                  FROM theplot.generation_edges AS e
                  INNER JOIN theplot.generation_nodes AS dep ON dep.id = e.from_node_id
                  WHERE e.to_node_id = gn.id AND dep.status <> @depDone
                )
              ORDER BY gn.date_created
              FOR UPDATE OF gn SKIP LOCKED
              LIMIT 1
            )
            UPDATE theplot.generation_nodes AS gn
            SET status = @setRunning,
                lease_worker_id = @worker,
                lease_expires_at_utc = @leaseEnd,
                date_last_modified = @now
            FROM next
            WHERE gn.id = next.id
            RETURNING gn.id;
            """;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        var npConn = (NpgsqlConnection)conn;
        var npTx = (NpgsqlTransaction)((DbTransaction)tx.GetDbTransaction());

        Guid? claimedId = null;
        await using (var cmd = new NpgsqlCommand(sql, npConn, npTx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("phase", phase));
            cmd.Parameters.Add(new NpgsqlParameter("runStatus", runRunning));
            cmd.Parameters.Add(new NpgsqlParameter("nodeRunning", nodeRunning));
            cmd.Parameters.Add(new NpgsqlParameter("depDone", depDone));
            cmd.Parameters.Add(new NpgsqlParameter("setRunning", setRunning));
            cmd.Parameters.Add(
                new NpgsqlParameter("now", DateTime.SpecifyKind(now, DateTimeKind.Unspecified)));
            cmd.Parameters.Add(new NpgsqlParameter("worker", workerId));
            cmd.Parameters.Add(
                new NpgsqlParameter("leaseEnd", DateTime.SpecifyKind(leaseEnd, DateTimeKind.Unspecified)));

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is Guid g)
            {
                claimedId = g;
            }
        }

        if (claimedId is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }

        var nodeId = claimedId.Value;

        var nextAttempt = await db.GenerationAttempts
            .Where(a => a.GenerationNodeId == nodeId)
            .Select(a => (int?)a.AttemptNumber)
            .MaxAsync(ct) ?? 0;

        var attempt = GenerationAttempt.Create(nodeId, nextAttempt + 1);
        attempt.MarkRunning();
        db.GenerationAttempts.Add(attempt);

        var node = await db.GenerationNodes.FirstAsync(n => n.Id == nodeId, ct);
        node.Claim(workerId, leaseEnd, attempt.Id);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ClaimedGenerationWork(nodeId, attempt.Id);
    }
}
