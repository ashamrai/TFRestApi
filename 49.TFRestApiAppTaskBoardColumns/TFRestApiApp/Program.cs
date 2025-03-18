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
using Microsoft.TeamFoundation.Core.WebApi.Types;
using System.Linq;
using Microsoft.TeamFoundation.Work.WebApi.Contracts.Taskboard;
using System.Collections.Generic;

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
                string TeamProjectName = "<Team Project Name>";
                string TeamName = "<Team Name>";

                ConnectWithPAT(TFUrl, UserPAT);                

                UpdateTaskBoardColumns(TeamProjectName, TeamName);

                ViewColumns(TeamProjectName, TeamName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// View all columns on team task board
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        private static void ViewColumns(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);
            var columns = WorkClient.GetColumnsAsync(teamContext).Result;

            foreach (var column in columns.Columns)
            {
                Console.WriteLine("{0} - {1} Mappings:", column.Order, column.Name);
                foreach (var mapping in column.Mappings)
                {
                    Console.WriteLine("{0} - {1}", mapping.WorkItemType, mapping.State);
                }
            }
        }

        /// <summary>
        /// Update columns on team task board
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TeamName"></param>
        private static void UpdateTaskBoardColumns(string TeamProjectName, string TeamName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName, TeamName);

            List<UpdateTaskboardColumn> columns = new List<UpdateTaskboardColumn>();

            columns.Add(NewColumn(0, "To Do", "Task", "New"));
            columns.Add(NewColumn(1, "Active", "Task", "Active"));
            columns.Add(NewColumn(2, "Done", "Task", "Closed"));

            WorkClient.UpdateColumnsAsync(columns.ToArray(), teamContext).Wait();
        }

        private static UpdateTaskboardColumn NewColumn(int order, string name, string workItem, string state)
        {
            TaskboardColumnMapping taskboardColumnMapping = new TaskboardColumnMapping();
            taskboardColumnMapping.WorkItemType = workItem;
            taskboardColumnMapping.State = state;

            UpdateTaskboardColumn taskboardColumn = new UpdateTaskboardColumn();
            taskboardColumn.Order = order;
            taskboardColumn.Name = name;
            taskboardColumn.Mappings = new TaskboardColumnMapping[] { taskboardColumnMapping };

            return taskboardColumn;
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
