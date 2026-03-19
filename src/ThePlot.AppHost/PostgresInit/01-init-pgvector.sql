-- Create theplot-db database and enable pgvector extension.
-- Runs at container startup; Aspire's AddDatabase will skip create if already exists.
CREATE DATABASE "theplot-db";

\connect "theplot-db"

CREATE EXTENSION IF NOT EXISTS vector;
