using System.Collections.Concurrent;
using System.Net;

using ISRORCert.Model;
using ISRORCert.Network;

namespace ISRORCert.Logic
{
    /// <summary>
    /// Keeps track of the connected server modules, for the status report.
    /// </summary>
    internal class SessionRegistry
    {
        public sealed class Session
        {
            public Guid Guid { get; init; }
            public IPEndPoint? EndPoint { get; init; }
            public DateTimeOffset ConnectedAt { get; init; }

            /// <summary>
            /// The server body this connection was certified as, once it sent its certificate request.
            /// </summary>
            public ServerBody? Body { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

        public void Add(AsyncContext context, IPEndPoint? endPoint, DateTimeOffset now) =>
            _sessions[context.Guid] = new Session { Guid = context.Guid, EndPoint = endPoint, ConnectedAt = now };

        public void Remove(AsyncContext context) => _sessions.TryRemove(context.Guid, out _);

        public void SetCertifiedBody(AsyncContext context, ServerBody body)
        {
            if (_sessions.TryGetValue(context.Guid, out var session))
                session.Body = body;
        }

        public IReadOnlyList<Session> Snapshot() => _sessions.Values.OrderBy(p => p.ConnectedAt).ToList();
    }
}
