namespace Ultra.Exceptions;

public interface IHasGrpcClientError
{
    GrpcErrorDescriptor ToGrpcError();
}
