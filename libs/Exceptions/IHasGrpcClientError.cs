namespace ThePlot.Exceptions;

public interface IHasGrpcClientError
{
    GrpcErrorDescriptor ToGrpcError();
}
