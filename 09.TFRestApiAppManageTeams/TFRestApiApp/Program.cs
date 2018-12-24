using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        //static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for azure devops 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = ""; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=vsts


        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static TeamHttpClient TeamClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                string teamProject = ""; // team project for tests
                string team = ""; // team to get info
                string tempTeam = ""; // temporary team to create and remove

                ConnectWithDefaultCreds(TFUrl);
                //ConnectWithPAT(TFUrl, UserPAT);

                GetTeams(teamProject);
                GetTeamInfo(teamProject, team);
                CreateNewTeam(teamProject, tempTeam);
                DeleteTeam(teamProject, tempTeam);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }                         
        
        /// <summary>
        /// Get all teams
        /// </summary>
        /// <param name="TeamProjectName"></param>
        static void GetTeams(string TeamProjectName)
        {
            TeamProject project = ProjectClient.GetProject(TeamProjectName).Result;

            Console.WriteLine("Teams for Project: " + project.Name);
            Console.WriteLine("Default Team Name: " + project.DefaultTeam.Name);

            List<WebApiTeam> teams = TeamClient.GetTeamsAsync(TeamProjectName).Result;

            Console.WriteLine("Project Teams:");

            foreach (WebApiTeam team in teams) Console.WriteLine(team.Name);
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
        /// Remove an existing team
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void DeleteTeam(string TeamProjectName, string TeamName)
        {
            Console.WriteLine("Delete the team '{0}' in the team project '{1}'", TeamName, TeamProjectName);
            TeamClient.DeleteTeamAsync(TeamProjectName, TeamName).SyncResult();
            Console.WriteLine("Comleted");
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
