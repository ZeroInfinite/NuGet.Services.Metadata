﻿using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security.OpenIdConnect;
using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Models;
using PublishTestDriverWebSite.Utils;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace PublishTestDriverWebSite.Controllers
{
    [Authorize]
    public class ValidateController : Controller
    {
        private string nugetPublishServiceBaseAddress = ConfigurationManager.AppSettings["nuget:PublishServiceBaseAddress"];
        private string nugetPublishServiceResourceId = ConfigurationManager.AppSettings["nuget:PublishServiceResourceId"];
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        public async Task<ActionResult> Index()
        {
            try
            {
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
                ClientCredential credential = new ClientCredential(clientId, appKey);

                AuthenticationResult authenticationResult = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetPublishServiceBaseAddress + "/domains");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    JArray result = JArray.Parse(json);

                    PublishModel model = new PublishModel();
                    foreach (string s in result.Values().Select(jtoken => jtoken.ToString()))
                    {
                        model.Domains.Add(s);
                    }

                    return View(model);
                }
                else
                {
                    return View(new PublishModel { Message = "Unable to load list of domains" });
                }
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                //TODO: this isn't quite right
                HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                return View(new PublishModel { Message = "AuthorizationRequired" });
            }
        }

        [HttpPost]
        public async Task<ActionResult> CheckAccess(string baseId, string relativeId)
        {
            try
            {
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
                ClientCredential credential = new ClientCredential(clientId, appKey);

                AuthenticationResult result = await authContext.AcquireTokenSilentAsync(nugetPublishServiceResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

                string id = string.Format("{0}/{1}", baseId ?? string.Empty, relativeId ?? string.Empty);

                string query = string.Format("?id={0}", id.ToLowerInvariant());

                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, nugetPublishServiceBaseAddress + "/checkaccess" + query);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

                HttpResponseMessage response = await client.SendAsync(request);

                string message = null;
                if (response.IsSuccessStatusCode)
                {
                    JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                    message = publishServiceResponse["message"].ToString();
                }
                else
                {
                    try
                    {
                        JObject publishServiceResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
                        string error = publishServiceResponse["error"].ToString();
                        message = string.Format("checkaccess error \"{0}\"", error);
                    }
                    catch { }
                }

                return View(new CheckAccessModel { Id = id, Message = message ?? string.Format("{0} with no further details available", response.StatusCode) });
            }
            catch (AdalSilentTokenAcquisitionException)
            {
                //TODO: this isn't quite right
                HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                return View(new CheckAccessModel { Message = "AuthorizationRequired" });
            }
        }
    }
}