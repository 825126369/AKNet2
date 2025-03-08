using System.Net.Quic;
using System.Net.Security;
using System.Net;

namespace OtherTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Do();
        }

        static async void Do()
        {
            // First, check if QUIC is supported.
            if (!QuicListener.IsSupported)
            {
                Console.WriteLine("QUIC is not supported, check for presence of libmsquic and support of TLS 1.3.");
                return;
            }

            // Share configuration for each incoming connection.
            // This represents the minimal configuration necessary.
            var serverConnectionOptions = new QuicServerConnectionOptions
            {
                // Used to abort stream if it's not properly closed by the user.
                // See https://www.rfc-editor.org/rfc/rfc9000#section-20.2
                DefaultStreamErrorCode = 0x0A, // Protocol-dependent error code.

                // Used to close the connection if it's not done by the user.
                // See https://www.rfc-editor.org/rfc/rfc9000#section-20.2
                DefaultCloseErrorCode = 0x0B, // Protocol-dependent error code.

                // Same options as for server side SslStream.
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    // Specify the application protocols that the server supports. This list must be a subset of the protocols specified in QuicListenerOptions.ApplicationProtocols.
                    ApplicationProtocols = [new SslApplicationProtocol("protocol-name")],
                    // Server certificate, it can also be provided via ServerCertificateContext or ServerCertificateSelectionCallback.
                    //ServerCertificate = AKNet.Common.X509CertTool.GenerateManualCertificate()
                }
            };

            // Initialize, configure the listener and start listening.
            var listener = await QuicListener.ListenAsync(new QuicListenerOptions
            {
                // Define the endpoint on which the server will listen for incoming connections. The port number 0 can be replaced with any valid port number as needed.
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
                // List of all supported application protocols by this listener.
                ApplicationProtocols = [new SslApplicationProtocol("protocol-name")],
                // Callback to provide options for the incoming connections, it gets called once per each connection.
                ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(serverConnectionOptions)
            });

            // Accept and process the connections.
            while (true)
            {
                // Accept will propagate any exceptions that occurred during the connection establishment,
                // including exceptions thrown from ConnectionOptionsCallback, caused by invalid QuicServerConnectionOptions or TLS handshake failures.
                var connection = await listener.AcceptConnectionAsync();

                // Process the connection...
            }

            // When finished, dispose the listener.
            await listener.DisposeAsync();
        }
    }
}
