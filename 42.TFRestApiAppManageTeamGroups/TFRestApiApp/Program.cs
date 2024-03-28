using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.PermissionsReport.Client;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static TeamHttpClient TeamClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static PermissionsReportHttpClient PermissionsReportClient;
        static IdentityHttpClient IdentityClient;

        static void Main(string[] args)
        {
            string TeamProjectName = "<Team Project Name>";
            string TeamName = "<Team Name>";
            string userDisplayName = "<Display Name of an existing user>";

            ConnectWithPAT(TFUrl, UserPAT);

            CreateTeamGroup(TeamProjectName, TeamName, userDisplayName);

            ListTeamProjectGroups(TeamProjectName);
            GetTeamInfo(TeamProjectName, TeamName);

            RemoveTeamGroup(TeamProjectName, TeamName, userDisplayName);
        }

        /// <summary>
        /// Remove a user from a group and remove a group
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        /// <param name="userDisplayName"></param>
        private static void RemoveTeamGroup(string TeamProjectName, string TeamName, string userDisplayName)
        {
            var identities = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, $@"[{TeamProjectName}]\{TeamName}").Result;
            var users = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, userDisplayName).Result;
            IdentityClient.RemoveMemberFromGroupAsync(identities[0].Descriptor, users[0].Descriptor).Wait();
            IdentityClient.DeleteGroupAsync(identities[0].Descriptor).Wait();
        }

        /// <summary>
        /// Create a new team and add a user through identity client
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        /// <param name="userDisplayName"></param>
        private static void CreateTeamGroup(string TeamProjectName, string TeamName, string userDisplayName)
        {
            CreateNewTeam(TeamProjectName, TeamName);

            var identities = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, $@"[{TeamProjectName}]\{TeamName}").Result;
            var users = IdentityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, userDisplayName).Result;

            IdentityClient.AddMemberToGroupAsync(identities[0].Descriptor, users[0].Id).Wait();
        }

        /// <summary>
        /// Show all groups through identity client
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListTeamProjectGroups(string TeamProjectName)
        {
            var scopeTeamProject = IdentityClient.GetScopeAsync($@"[{TeamProjectName}]").Result;

            var groups = IdentityClient.ListGroupsAsync(new Guid[] { scopeTeamProject.Id }).Result;

            foreach (var group in groups)
                Console.WriteLine($@"Team project Group {group.DisplayName}");
        }

        /// <summary>
        /// Create a new team
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void CreateNewTeam(string TeamProjectName, string TeamName)
        {
            WebApiTeam newTeam = new WebApiTeam();

            newTeam.Name = TeamName;
            newTeam.Description = "Created from a command line";

            newTeam = TeamClient.CreateTeamAsync(newTeam, TeamProjectName).Result;

            Console.WriteLine("The new team '{0}' has been created in the team project '{1}'", newTeam.Name, newTeam.ProjectName);
        }

        /// <summary>
        /// Get detailed team information
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void GetTeamInfo(string TeamProjectName, string TeamName)
        {
            WebApiTeam team = TeamClient.GetTeamAsync(TeamProjectName, TeamName).Result;

            Console.WriteLine("Team name: " + team.Name);
            Console.WriteLine("Team description:\n{0}\n", team.Description);

            List<TeamMember> teamMembers = TeamClient.GetTeamMembersWithExtendedPropertiesAsync(TeamProjectName, TeamName).Result;

            string teamAdminName = (from tm in teamMembers where tm.IsTeamAdmin == true select tm.Identity.DisplayName).FirstOrDefault();

            if (teamAdminName != null) Console.WriteLine("Team Administrator:" + teamAdminName);

            Console.WriteLine("Team members:");
            foreach (TeamMember teamMember in teamMembers)
                if (!teamMember.IsTeamAdmin) Console.WriteLine(teamMember.Identity.DisplayName);
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
            PermissionsReportClient = Connection.GetClient<PermissionsReportHttpClient>();
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
