using System.Collections.Generic;
using Pug.Collections.Generic.Extensions;

namespace Pug.Application.Security
{
    public class BasicPrincipalIdentity : IPrincipalIdentity
    {
        public BasicPrincipalIdentity(string identifier, string name, bool isAuthenticated, string authenticationType, IDictionary<string, string> attributes)
        {
            Identifier = identifier;
            Name = name;
            AuthenticationType = authenticationType;
            IsAuthenticated = isAuthenticated;
            Attributes = attributes.ReadOnly();
        }

        public IDictionary<string, string> Attributes { get; private set; }

        public string Identifier { get; private set; }

        public string AuthenticationType { get; private set; }

        public bool IsAuthenticated { get; private set; }

        public string Name { get; private set; }

        public void Dispose()
        {
            // not required
        }

        public bool Equals(ICredentials other)
        {
            return other != null && other.Identifier == Identifier;
        }
    }
}
