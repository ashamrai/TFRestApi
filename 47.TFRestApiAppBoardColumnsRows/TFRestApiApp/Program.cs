using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using System.Security.AccessControl;

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
        static ReleaseHttpClient ReleaseClient;
        static WorkHttpClient WorkClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Team project name>";

                ConnectWithPAT(TFUrl, UserPAT);

                ListAllBoards(TeamProjectName);

                ListColumns(TeamProjectName);
                UpdateColumns(TeamProjectName);

                ListRows(TeamProjectName);
                UpdateRows(TeamProjectName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Update board rows
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void UpdateRows(string TeamProjectName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName);
            string boardName = "Stories";

            var rows = WorkClient.GetBoardRowsAsync(teamContext, boardName).Result;

            var newRow = new BoardRow();
            newRow.Name = "Option1";

            rows.Add(newRow);

            WorkClient.UpdateBoardRowsAsync(rows, teamContext, boardName).Wait();
        }

        /// <summary>
        /// View all board rows
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListRows(string TeamProjectName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName);
            string boardName = "Stories";

            var rows = WorkClient.GetBoardRowsAsync(teamContext, boardName).Result;

            foreach (var row in rows)
            {
                if (row.Id != Guid.Empty)
                    Console.WriteLine("{0} - {1}", row.Name, row.Color);
            }
        }

        /// <summary>
        /// Update board columns
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void UpdateColumns(string TeamProjectName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName);
            string boardName = "Stories";
            string columnToCopy = "Active";

            var columns = WorkClient.GetBoardColumnsAsync(teamContext, boardName).Result;

            var activeColumn = (from x in columns where x.Name == columnToCopy select x).FirstOrDefault();

            var newColumn = new BoardColumn();
            newColumn.Name = "Testing";
            newColumn.ColumnType = BoardColumnType.InProgress;
            newColumn.IsSplit = true;
            newColumn.ItemLimit = 10;

            newColumn.StateMappings = new Dictionary<string, string>();

            foreach (var mapping in activeColumn.StateMappings)
                newColumn.StateMappings.Add(mapping.Key, mapping.Value);

            columns.Insert(columns.Count - 2, newColumn);
            WorkClient.UpdateBoardColumnsAsync(columns, teamContext, boardName).Wait();
        }

        /// <summary>
        /// View all board columns
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListColumns(string TeamProjectName)
        {
            TeamContext teamContext = new TeamContext(TeamProjectName);
            string boardName = "Stories";

            var columns = WorkClient.GetBoardColumnsAsync(teamContext, boardName).Result;

            foreach (var column in columns)
            {
                Console.WriteLine("{0} : {1} : {2}", column.Name, column.ColumnType.ToString(), (column.IsSplit == true) ? "split" : "without split");
                foreach (var mapping in column.StateMappings)
                {
                    Console.WriteLine("{0} - {1}", mapping.Key, mapping.Value);
                }
            }
        }

        /// <summary>
        /// View all project boards
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListAllBoards(string TeamProjectName)
        {
            var teams = TeamClient.GetTeamsAsync(TeamProjectName).Result;

            foreach (var team in teams)
            {
                Console.WriteLine("Board for team: {0}", team.Name);
                TeamContext teamContext = new TeamContext(TeamProjectName, team.Name);

                var boards = WorkClient.GetBoardsAsync(teamContext).Result;

                foreach (var board in boards)
                {
                    Console.WriteLine("{0} : {1}", board.Id, board.Name);
                }

                Console.WriteLine();
            }
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
            ReleaseClient = Connection.GetClient<ReleaseHttpClient>();
            WorkClient = Connection.GetClient<WorkHttpClient>();
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
