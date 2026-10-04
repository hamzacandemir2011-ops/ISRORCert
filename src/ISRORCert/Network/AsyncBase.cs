using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System;

namespace ISRORCert.Network
{
    public abstract class AsyncBase
    {
        private readonly List<AsyncState> states;

        internal ILogger Logger { get; }

        protected AsyncBase(ILogger? logger = null)
        {
            states = new List<AsyncState>();
            Logger = logger ?? NullLogger.Instance;
        }

        public int ConnectionCount
        {
            get
            {
                lock (states)
                    return states.Count;
            }
        }

        public void Tick()
        {
            lock (states)
            {
                foreach (AsyncState state in states)
                {
                    try
                    {
                        state.Context.Interface.OnTick(state.Context);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "OnTick failed for {Guid}", state.Context.Guid);
                    }
                }
            }
        }

        public void AddState(AsyncState state)
        {
            lock (states)
            {
                states.Add(state);
            }
        }

        public void RemoveState(AsyncState state)
        {
            lock (states)
            {
                states.Remove(state);
            }
        }
    }
}
