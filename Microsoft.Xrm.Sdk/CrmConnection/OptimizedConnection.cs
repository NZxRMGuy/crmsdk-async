using Microsoft.Crm.Services.Utility;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace CrmConnection
{
    public abstract class XrmServiceManagerBase { }

    public sealed class AutoRefreshSecurityToken<TP, TS>
        where TP : ServiceProxy<TS>
        where TS : class
    {
        private TP _proxy;

        public AutoRefreshSecurityToken(TP proxy)
        {
            if (null == proxy)
            {
                throw new ArgumentNullException("proxy");
            }

            this._proxy = proxy;
        }


        public void PrepareCredentials()
        {
            if (null == this._proxy.ClientCredentials)
            {
                return;
            }

            switch (this._proxy.ServiceConfiguration.AuthenticationType)
            {
                case AuthenticationProviderType.ActiveDirectory:
                    this._proxy.ClientCredentials.UserName.UserName = null;
                    this._proxy.ClientCredentials.UserName.Password = null;
                    break;
                case AuthenticationProviderType.Federation:
                case AuthenticationProviderType.OnlineFederation:
                case AuthenticationProviderType.LiveId:
                    this._proxy.ClientCredentials.Windows.ClientCredential = null;
                    break;
                default:
                    return;
            }
        }

        public void RenewTokenIfRequired()
        {
            if (this._proxy.ServiceConfiguration.AuthenticationType != AuthenticationProviderType.ActiveDirectory
                && this._proxy.SecurityTokenResponse != null
                && DateTime.UtcNow.AddMinutes(15) >= this._proxy.SecurityTokenResponse.Response.Lifetime.Expires)
            {
                try
                {
                    this._proxy.Authenticate();
                }
                catch (CommunicationException)
                {
                    if (null == this._proxy.SecurityTokenResponse || DateTime.UtcNow >= this._proxy.SecurityTokenResponse.Response.Lifetime.Expires)
                    {
                        throw;
                    }
                }
            }
        }
    }

    public sealed class ManagedTokenOrganizationServiceProxy : OrganizationServiceProxy
    {
        private AutoRefreshSecurityToken<OrganizationServiceProxy, IOrganizationService> _proxyManager;

        public ManagedTokenOrganizationServiceProxy(IServiceManagement<IOrganizationService> serviceManagement, SecurityTokenResponse securityTokenResponse)
            : base(serviceManagement, securityTokenResponse)
        {
            this._proxyManager = new AutoRefreshSecurityToken<OrganizationServiceProxy, IOrganizationService>(this);
        }

        public ManagedTokenOrganizationServiceProxy(IServiceManagement<IOrganizationService> serviceManagement, ClientCredentials credentials)
            : base(serviceManagement, credentials)
        {
            this._proxyManager = new AutoRefreshSecurityToken<OrganizationServiceProxy, IOrganizationService>(this);
        }

        protected override void AuthenticateCore()
        {
            this._proxyManager.PrepareCredentials();
            base.AuthenticateCore();
        }

        protected override void ValidateAuthentication()
        {
            this._proxyManager.RenewTokenIfRequired();
            base.ValidateAuthentication();
        }
    }

    public class OrganizationServiceManager : XrmServiceManager<IOrganizationService, ManagedTokenOrganizationServiceProxy>
    {
        public OrganizationServiceManager(string orgServiceUrl, AuthenticationCredentials credentials)
            : base(new Uri(orgServiceUrl), credentials)
        {
        }

        public OrganizationServiceManager(string orgServiceUrl, string username, string password, string domain = null, Uri homeRealm = null)
            : base(new Uri(orgServiceUrl), username, password, domain, homeRealm)
        {
        }

        public OrganizationServiceManager(IServiceManagement<IOrganizationService> serviceManagement, AuthenticationCredentials credentials)
            : base(serviceManagement, credentials)
        {
        }

        public OrganizationServiceManager(IServiceManagement<IOrganizationService> serviceManagement)
            : base(serviceManagement)
        {
        }

        public OrganizationServiceManager(string orgServiceUrl)
            : base(ServiceConfigurationFactory.CreateManagement<IOrganizationService>(new Uri(orgServiceUrl)))
        {
        }
    }

    public abstract class XrmServiceManager<TS, TP> : XrmServiceManagerBase
    where TS : class
    where TP : ServiceProxy<TS>
    {

        private XrmServiceManager()
        {
            throw new NotImplementedException("Default constructor not implemented");
        }

        protected XrmServiceManager(Uri serviceUri, AuthenticationCredentials credentials)
        {
            this.ServiceUri = serviceUri;
            this.ServiceManagement = ServiceConfigurationFactory.CreateManagement<TS>(serviceUri);

            Authenticate(credentials);
        }

        protected XrmServiceManager(Uri serviceUri, string username, string password, string domain = null, Uri homeRealm = null)
        {
            this.ServiceUri = serviceUri;
            this.ServiceManagement = ServiceConfigurationFactory.CreateManagement<TS>(serviceUri);

            Authenticate(username, password, domain, homeRealm);
        }

        protected XrmServiceManager(IServiceManagement<TS> serviceManagement, AuthenticationCredentials credentials)
        {
            this.ServiceUri = serviceManagement.CurrentServiceEndpoint.Address.Uri;
            this.ServiceManagement = serviceManagement;

            this.Credentials = credentials != null
                ? credentials
                : this.DefaultCredentials;

            RequestSecurityToken();
        }

        protected XrmServiceManager(IServiceManagement<TS> serviceManagement)
            : this(serviceManagement, null)
        {
        }

        private AuthenticationCredentials defaultCredentials;


        private IServiceManagement<TS> ServiceManagement { get; set; }
        private AuthenticationCredentials Credentials { get; set; }
        private AuthenticationCredentials AuthenticatedCredentials { get; set; }

        private AuthenticationCredentials DefaultCredentials
        {
            get
            {
                if (this.defaultCredentials == null)
                {
                    this.defaultCredentials = new AuthenticationCredentials()
                    {
                        ClientCredentials = new ClientCredentials()
                        {
                            Windows =
                            {
                                ClientCredential = CredentialCache.DefaultNetworkCredentials
                            }
                        }
                    };
                }

                return this.defaultCredentials;
            }
        }

        private bool IsOrganizationService
        {
            get
            {
                return typeof(TS).Equals(typeof(IOrganizationService));
            }
        }

        private bool HasToken
        {
            get
            {
                if (this.AuthenticatedCredentials != null && this.AuthenticatedCredentials.SecurityTokenResponse != null)
                {
                    return true;
                }

                return false;
            }
        }

        private bool TokenExpired
        {
            get
            {
                if (this.HasToken && this.AuthenticatedCredentials.SecurityTokenResponse.Response.Lifetime.Expires <= DateTime.UtcNow.AddMinutes(15))
                {
                    return true;
                }

                return false;
            }
        }

        public Uri ServiceUri { get; private set; }

        public AuthenticationProviderType AuthenticationType
        {
            get
            {
                return this.ServiceManagement.AuthenticationType;
            }
        }

        public bool IsCrmOnline
        {
            get
            {
                return this.AuthenticationType == AuthenticationProviderType.LiveId
                    || this.AuthenticationType == AuthenticationProviderType.OnlineFederation;
            }
        }


        private void Authenticate(AuthenticationCredentials credentials)
        {
            this.Credentials = credentials;

            RequestSecurityToken();
        }

        private void Authenticate(string username, string password, string domain = null, Uri homeRealmUri = null)
        {
            switch (this.AuthenticationType)
            {
                case AuthenticationProviderType.ActiveDirectory:
                    this.AuthenticateCredentials(new ClientCredentials() { Windows = { ClientCredential = new NetworkCredential(username, password, domain) } });
                    return;

                case AuthenticationProviderType.Federation:
                case AuthenticationProviderType.OnlineFederation:
                    this.AuthenticateFederatedRealmCredentials(new ClientCredentials() { UserName = { UserName = username, Password = password } }, homeRealmUri);
                    return;

                case AuthenticationProviderType.LiveId:
                    this.AuthenticateLiveIdCredentials(new ClientCredentials() { UserName = { UserName = username, Password = password } });
                    return;

                default:
                    throw new NotSupportedException(string.Format("{0} authentication type is not supported", this.ServiceManagement.AuthenticationType));
            }
        }

        private void AuthenticateSingleSignOnCredentials(string userPrincipalName)
        {
            this.Credentials = new AuthenticationCredentials()
            {
                UserPrincipalName = userPrincipalName
            };

            RequestSecurityToken();
        }

        private void AuthenticateCredentials(ClientCredentials clientCredentials)
        {
            this.Credentials = new AuthenticationCredentials()
            {
                ClientCredentials = clientCredentials
            };

            RequestSecurityToken();
        }

        private void AuthenticateFederatedRealmCredentials(ClientCredentials clientCredentials, Uri HomeRealmUri)
        {
            this.Credentials = new AuthenticationCredentials()
            {
                ClientCredentials = clientCredentials,
                HomeRealm = HomeRealmUri
            };

            RequestSecurityToken();
        }

        private void AuthenticateLiveIdCredentials(ClientCredentials clientCredentials)
        {
            var deviceCredentials = this.ServiceManagement.IssuerEndpoints.ContainsKey("Username")
                ? DeviceIdManager.LoadOrRegisterDevice(this.ServiceManagement.IssuerEndpoints["Username"].IssuerAddress.Uri)
                : DeviceIdManager.LoadOrRegisterDevice();

            AuthenticateLiveIdCredentials(clientCredentials, deviceCredentials);
        }

        private void AuthenticateLiveIdCredentials(ClientCredentials clientCredentials, ClientCredentials deviceCredentials)
        {
            this.Credentials = new AuthenticationCredentials()
            {
                ClientCredentials = clientCredentials,
                SupportingCredentials = new AuthenticationCredentials() { ClientCredentials = deviceCredentials }
            };

            RequestSecurityToken();
        }

        private void RefreshSecurityToken()
        {
            this.Credentials.SecurityTokenResponse = null;
            this.AuthenticatedCredentials = null;

            RequestSecurityToken();
        }

        private void RequestSecurityToken()
        {
            if (this.AuthenticationType != AuthenticationProviderType.ActiveDirectory
                && this.Credentials != null)
            {
                this.AuthenticatedCredentials = this.ServiceManagement.Authenticate(this.Credentials);
            }
        }

        protected T GetProxy<T>()
        {
            switch (this.ServiceManagement.AuthenticationType)
            {

                case AuthenticationProviderType.ActiveDirectory:
                    return (T)typeof(T)
                        .GetConstructor(new Type[]
                        {
                            typeof(IServiceManagement<TS>),
                            typeof(ClientCredentials)
                        })
                            .Invoke(new object[]
                        {
                            this.ServiceManagement,
                            this.Credentials.ClientCredentials
                        });

                case AuthenticationProviderType.Federation:
                case AuthenticationProviderType.OnlineFederation:
                case AuthenticationProviderType.LiveId:
                    if (!this.HasToken
                        || this.TokenExpired)
                    {
                        RefreshSecurityToken();
                    }

                    return (T)typeof(T)
                        .GetConstructor(new Type[]
                        {
                            typeof(IServiceManagement<TS>),
                            typeof(SecurityTokenResponse)
                        })
                            .Invoke(new object[]
                        {
                            this.ServiceManagement,
                            this.AuthenticatedCredentials.SecurityTokenResponse
                        });

                default:
                    throw new NotSupportedException(string.Format("{0} authentication type is not supported", this.ServiceManagement.AuthenticationType));
            }
        }

        public TP GetProxy()
        {
            return this.GetProxy<TP>();
        }
    }
}
