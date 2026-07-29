using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

[assembly: OwinStartup(typeof(PlataformaWeb.App_Start.Startup))]

namespace PlataformaWeb.App_Start
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }

        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = CookieAuthenticationDefaults.AuthenticationType,
                CookieName = ".AspNet.Cookies",
                CookieDomain = string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["CookieDomain"]) ? null : ConfigurationManager.AppSettings["CookieDomain"],
                CookieSecure = CookieSecureOption.SameAsRequest,
                CookieSameSite = SameSiteMode.Lax,
                CookieManager = new Microsoft.Owin.Host.SystemWeb.SystemWebCookieManager(),
            });
        }

    }
}