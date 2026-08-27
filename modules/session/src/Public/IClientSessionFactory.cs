namespace Lumio.Client.Session
{
    public interface IClientSessionFactory
    {
        SessionCommandResult Create(in ClientSessionDependencies dependencies, out IClientSession session);
    }

    public sealed class ClientSessionFactory : IClientSessionFactory
    {
        public SessionCommandResult Create(in ClientSessionDependencies dependencies, out IClientSession session)
        {
            session = new ClientSession(dependencies);
            return new SessionCommandResult(true);
        }
    }
}
