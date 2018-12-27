using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
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
        static WorkHttpClient WorkClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                string teamProject = ""; // team project for tests
                string team = teamProject + " Team"; // team to tests (default team)                

                ConnectWithDefaultCreds(TFUrl);
                //ConnectWithPAT(TFUrl, UserPAT);

                AddTeamIterations(teamProject, team);
                DeleteTeamIteration(teamProject, team);
                AddTeamAreas(teamProject, team);
                GetTeamSettings(teamProject, team);
                GetTeamAreas(teamProject, team);
                GetTeamIterations(teamProject, team);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Add existing iteraions to a team
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void AddTeamAreas(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            Console.WriteLine("Add areas to the team " + TeamName);

            string[] areas = { @"Application\WinClient", @"Application\WebClient" };

            TeamFieldValues currentTeamAreas = WorkClient.GetTeamFieldValuesAsync(teamContext).Result; // get current areas

            TeamFieldValuesPatch teamAreasPatch = new TeamFieldValuesPatch();
            
            List<TeamFieldValue> newTeamAreas = new List<TeamFieldValue>(currentTeamAreas.Values); // just copy old areas. Here we may remove unneeded areas

            foreach (string area in areas)
                newTeamAreas.Add(new TeamFieldValue { Value = TeamProjectName + "\\" + area, IncludeChildren = false }); // add new areas

            teamAreasPatch.DefaultValue = currentTeamAreas.DefaultValue;
            teamAreasPatch.Values = newTeamAreas;            

            TeamFieldValues updatedTeamAreas = WorkClient.UpdateTeamFieldValuesAsync(teamAreasPatch, teamContext).Result;

            Console.WriteLine("Areas has been added");
        }

        /// <summary>
        /// Add existing areas
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void AddTeamIterations(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            Console.WriteLine("Add iterations to the team " + TeamName);

            string[] iterations = { @"R2\R2.1\Ver1", @"R2\R2.1\Ver2" };

            foreach (string it in iterations)
            {
                WorkItemClassificationNode projectIt = WitClient.GetClassificationNodeAsync(TeamProjectName, TreeStructureGroup.Iterations, it).Result; // get iteration from project
                TeamSettingsIteration teamIt = WorkClient.PostTeamIterationAsync(new TeamSettingsIteration { Id = projectIt.Identifier }, teamContext).Result; //add iteration to a team by guid 
                Console.WriteLine("Added iteration " + teamIt.Name);
            }

        }

        /// <summary>
        /// Remove iteration from team settings
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void DeleteTeamIteration(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            Console.WriteLine("Remove iterations for the team " + TeamName);

            string iterationToRemove = @"R2\R2.1\Ver2";

            WorkItemClassificationNode projectIt = WitClient.GetClassificationNodeAsync(TeamProjectName, TreeStructureGroup.Iterations, iterationToRemove).Result;
            WorkClient.DeleteTeamIterationAsync(teamContext, projectIt.Identifier).SyncResult(); //delete iteration from team settings by guid
            Console.WriteLine("Removed iteration " + projectIt.Name);
        }

        /// <summary>
        /// Get general team settings
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void GetTeamSettings(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            TeamSetting teamSetting = WorkClient.GetTeamSettingsAsync(teamContext).Result;

            Console.WriteLine("Settings for the team " + TeamName);
            Console.WriteLine("Backlog Iteration    : " + teamSetting.BacklogIteration.Name);
            Console.WriteLine("Default Iteration    : " + teamSetting.DefaultIteration.Name);
            Console.WriteLine("Macro of Iteration   : " + teamSetting.DefaultIterationMacro);
            Console.WriteLine("Categories of backlog:");
            foreach(string bkey in teamSetting.BacklogVisibilities.Keys)
                if (teamSetting.BacklogVisibilities[bkey]) Console.WriteLine("\t" + bkey);
            Console.WriteLine("Working days         :");
            foreach (var wday in teamSetting.WorkingDays) Console.WriteLine("\t" + wday.ToString());             
        }

        /// <summary>
        /// Get team ares
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void GetTeamAreas(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            TeamFieldValues teamAreas = WorkClient.GetTeamFieldValuesAsync(teamContext).Result; //Get All Areas

            Console.WriteLine("Areas of the team " + TeamName);
            Console.WriteLine("Default Area: " + teamAreas.DefaultValue);

            Console.WriteLine("Team Areas  : ");
            foreach (TeamFieldValue teamField in teamAreas.Values)
                Console.WriteLine("\t" + teamField.Value + ((teamField.IncludeChildren)? " (include sub-areas)" : ""));
        }

        /// <summary>
        /// Get Team Iterations
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        static void GetTeamIterations(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            Console.WriteLine("Iterations of the team " + TeamName);

            TeamSettingsIteration currentiteration = (WorkClient.GetTeamIterationsAsync(teamContext, "Current").Result).FirstOrDefault(); // get a current iteration

            if (currentiteration != null)
                Console.WriteLine("Current iteration - {0} : {1}-{2}", currentiteration.Name, currentiteration.Attributes.StartDate, currentiteration.Attributes.FinishDate);

            List<TeamSettingsIteration> teamIterations = WorkClient.GetTeamIterationsAsync(teamContext).Result;   //get all iterations

            Console.WriteLine("Team Iterations: ");
            foreach (TeamSettingsIteration teamIteration in teamIterations)
                Console.WriteLine("{0} : {1} : {2}-{3}", teamIteration.Attributes.TimeFrame, teamIteration.Name, teamIteration.Attributes.StartDate, teamIteration.Attributes.FinishDate);
        }
        

        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            TeamClient = Connection.GetClient<TeamHttpClient>();
            WorkClient = Connection.GetClient<WorkHttpClient>();
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
