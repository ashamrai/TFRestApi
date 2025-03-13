using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Net;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Collections;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static TeamHttpClient TeamClient;
        static WorkHttpClient WorkClient;
        static IdentityHttpClient IdentityClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Project Name>";
                string userDisplayName = "<Display Name>";

                ConnectWithPAT(TFUrl, UserPAT);
                SendWorkItem(TeamProjectName, userDisplayName);
                SendWiql(TeamProjectName, userDisplayName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Send email for work item id
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="userDisplayName"></param>
        private static void SendWorkItem(string TeamProjectName, string userDisplayName)
        {
            var users = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, userDisplayName).Result;

            SendMailBody sendMailBody = new SendMailBody();
            sendMailBody.ids = new int[] { 691 };
            sendMailBody.fields = new string[] { "System.Title" };
            sendMailBody.sortFields = new string[] { "System.Id" };
            sendMailBody.message = new MailMessage();
            sendMailBody.message.Body = "One work item";
            sendMailBody.message.Subject = "Check work item";
            sendMailBody.message.To = new EmailRecipients();
            sendMailBody.message.To.EmailAddresses = new string[] { users[0].Properties.GetValue<string>("Mail", "") };
            sendMailBody.message.To.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.To.TfIds = new Guid[] { users[0].Id };
            sendMailBody.message.ReplyTo = new EmailRecipients();
            sendMailBody.message.ReplyTo.EmailAddresses = new string[] { };
            sendMailBody.message.ReplyTo.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.ReplyTo.TfIds = new Guid[] { };
            sendMailBody.message.CC = new EmailRecipients();
            sendMailBody.message.CC.EmailAddresses = new string[] { };
            sendMailBody.message.CC.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.CC.TfIds = new Guid[] { };

            WitClient.SendMailAsync(sendMailBody, TeamProjectName).Wait();
        }

        /// <summary>
        /// Send email for wiql
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="userDisplayName"></param>
        private static void SendWiql(string TeamProjectName, string userDisplayName)
        {
            var users = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, userDisplayName).Result;

            SendMailBody sendMailBody = new SendMailBody();
            sendMailBody.fields = new string[] { "System.Id", "System.Title", "System.AssignedTo", "System.State" };
            sendMailBody.sortFields = new string[] { "System.Id" };
            sendMailBody.wiql = $"SELECT [System.Id] FROM workitems WHERE [System.TeamProject] = '{TeamProjectName}' AND [System.WorkItemType] = 'User Story' AND [System.State] = 'Active'";
            sendMailBody.message = new MailMessage();
            sendMailBody.message.Body = "List of User Stories";
            sendMailBody.message.Subject = "Active user Stories";
            sendMailBody.message.To = new EmailRecipients();
            sendMailBody.message.To.EmailAddresses = new string[] { users[0].Properties.GetValue<string>("Mail", "") };
            sendMailBody.message.To.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.To.TfIds = new Guid[] { users[0].Id };
            sendMailBody.message.ReplyTo = new EmailRecipients();
            sendMailBody.message.ReplyTo.EmailAddresses = new string[] { };
            sendMailBody.message.ReplyTo.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.ReplyTo.TfIds = new Guid[] { };
            sendMailBody.message.CC = new EmailRecipients();
            sendMailBody.message.CC.EmailAddresses = new string[] { };
            sendMailBody.message.CC.UnresolvedEntityIds = new Guid[] { };
            sendMailBody.message.CC.TfIds = new Guid[] { };

            WitClient.SendMailAsync(sendMailBody, TeamProjectName).Wait();
        }


        #region create new connections

        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            TeamClient = Connection.GetClient<TeamHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
            WorkClient = Connection.GetClient<WorkHttpClient>();
            IdentityClient = Connection.GetClient<IdentityHttpClient>();
        }

        static void ConnectWithDefaultCreds(string ServiceURL)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssCredentials());
            InitClients(connection);
        }

        static void ConnectWithCustomCreds(string ServiceURL, string User, string Password)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new WindowsCredential(new NetworkCredential(User, Password)));
            InitClients(connection);
        }

        static void ConnectWithPAT(string ServiceURL, string PAT)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssBasicCredential(string.Empty, PAT));
            InitClients(connection);
        }
        #endregion
    }
}
