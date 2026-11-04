using System;
using System.Threading.Tasks;
using Grpc.Core;

namespace Training
{
    public static class TrainerShutdownSignal
    {
        private const string ServiceName = "communicator_objects.UnityToExternalProto";
        private const string MethodName = "Exchange";
        private const int Attempts = 2;
        private const int CallTimeoutMilliseconds = 3000;

        private static readonly byte[] ClosedMessage = { 0x0A, 0x03, 0x08, 0x90, 0x03 };

        private static readonly Marshaller<byte[]> Bytes = Marshallers.Create(value => value, value => value);
        private static readonly Method<byte[], byte[]> Exchange =
            new Method<byte[], byte[]>(MethodType.Unary, ServiceName, MethodName, Bytes, Bytes);

        public static Task<int> SendAsync(int port)
        {
            return Task.Run(() =>
            {
                var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
                int delivered = 0;
                try
                {
                    var invoker = new DefaultCallInvoker(channel);
                    for (int i = 0; i < Attempts; i++)
                    {
                        try
                        {
                            var options = new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(CallTimeoutMilliseconds));
                            invoker.BlockingUnaryCall(Exchange, null, options, ClosedMessage);
                            delivered++;
                        }
                        catch (RpcException e)
                        {
                            if (e.Status.StatusCode == StatusCode.DeadlineExceeded)
                                delivered++;
                            else
                                break;
                        }
                    }
                }
                finally
                {
                    channel.ShutdownAsync().Wait(CallTimeoutMilliseconds);
                }

                return delivered;
            });
        }
    }
}
