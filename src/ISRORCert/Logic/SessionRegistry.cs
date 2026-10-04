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

            /// <summary>
            /// The live connection (null in tests that build sessions by hand).
            /// </summary>
            public AsyncContext? Context { get; init; }
            public IPEndPoint? EndPoint { get; init; }
            public DateTimeOffset ConnectedAt { get; init; }

            /// <summary>
            /// The server body this connection was certified as, once it sent its certificate request.
            /// </summary>
            public ServerBody? Body { get; set; }
        }

        private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

        public SessionRegistry(CertificationManager certificationManager)
        {
            certificationManager.Reloaded += (_, next) => RemapBodies(next);
        }

        /// <summary>
        /// After a reload, point the sessions at the new server body objects (same ID).
        /// Bodies that no longer exist are cleared; the connection itself stays open.
        /// </summary>
        public void RemapBodies(CertificationData data)
        {
            foreach (var session in _sessions.Values)
            {
                if (session.Body is null)
                    continue;

                session.Body = data.TryGetServerBody(session.Body.ID, out var body) ? body : null;
            }
        }

        public void Add(AsyncContext context, IPEndPoint? endPoint, DateTimeOffset now) =>
            _sessions[context.Guid] = new Session { Guid = context.Guid, Context = context, EndPoint = endPoint, ConnectedAt = now };

        public void Remove(AsyncContext context) => _sessions.TryRemove(context.Guid, out _);

        public void SetCertifiedBody(AsyncContext context, ServerBody body)
        {
            if (_sessions.TryGetValue(context.Guid, out var session))
                session.Body = body;
        }

        public bool TryGet(Guid guid, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Session session) =>
            _sessions.TryGetValue(guid, out session);

        public IReadOnlyList<Session> Snapshot() => _sessions.Values.OrderBy(p => p.ConnectedAt).ToList();
    }
}
