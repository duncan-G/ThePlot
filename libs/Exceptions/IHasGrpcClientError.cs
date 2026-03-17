namespace Exceptions;

public interface IHasGrpcClientError
{
    GrpcErrorDescriptor ToGrpcError();
}
