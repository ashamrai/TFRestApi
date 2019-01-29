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
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "your_pat";

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                ConnectWithPAT(TFUrl, UserPAT);

                int wiIdToMove = -1; //update workitem id
                string teamProjectOld = "Team Project with Query", teamProjectNew = "New Team Project"; //update team projects
                string queryPath = "Shared Queries/Work Items to Move"; 

                //Move only one work item
                MoveWorkItem(wiIdToMove, teamProjectNew);

                //Move work items from a flat query result
                List<int> wis = RunStoredQuery(teamProjectOld, queryPath);
                foreach (int wiId in wis) MoveWorkItem(wiId, teamProjectNew);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }



        /// <summary>
        /// Run query and show result (only flat)
        /// </summary>
        /// <param name="wiqlStr">Wiql String</param>
        static List<int> GetQueryResult(string wiqlStr, string teamProject)
        {
            WorkItemQueryResult result = RunQueryByWiql(wiqlStr, teamProject);

            if (result != null)
            {
                if (result.WorkItems != null) // this is Flat List 
                    return (from wis in result.WorkItems select wis.Id).ToList();
                else Console.WriteLine("There is no query result");
            }

            return new List<int>();
        }

        /// <summary>
        /// Run Query with Wiql
        /// </summary>
        /// <param name="wiqlStr">Wiql String</param>
        /// <returns></returns>
        static WorkItemQueryResult RunQueryByWiql(string wiqlStr, string teamProject)
        {
            Wiql wiql = new Wiql();
            wiql.Query = wiqlStr;

            if (teamProject == "") return WitClient.QueryByWiqlAsync(wiql).Result;
            else return WitClient.QueryByWiqlAsync(wiql, teamProject).Result;
        }

        /// <summary>
        /// Run stored query on azure devops service
        /// </summary>
        /// <param name="project">Team Project Name</param>
        /// <param name="queryPath">Path to Query</param>
        static List<int> RunStoredQuery(string project, string queryPath)
        {
            QueryHierarchyItem query = WitClient.GetQueryAsync(project, queryPath, QueryExpand.Wiql).Result;

            string wiqlStr = query.Wiql;

            return GetQueryResult(wiqlStr, project);
        }

        /// <summary>
        /// Move work item to new team project
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="NewTeamProject"></param>
        /// <returns></returns>
        static int MoveWorkItem(int WIId, string NewTeamProject)
        {
            Dictionary<string, object> fields = new Dictionary<string, object>();

            fields.Add("System.TeamProject", NewTeamProject);
            fields.Add("System.AreaPath", NewTeamProject);
            fields.Add("System.IterationPath", NewTeamProject);

            var editedWI = UpdateWorkItem(WIId, fields);

            Console.WriteLine("Work item has been moved: " + editedWI.Id.Value);

            return editedWI.Id.Value;
        }

        /// <summary>
        /// Update a work item
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="Fields"></param>
        /// <returns></returns>
        static WorkItem UpdateWorkItem(int WIId, Dictionary<string, object> Fields)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var key in Fields.Keys)
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/" + key,
                    Value = Fields[key]
                });

            return WitClient.UpdateWorkItemAsync(patchDocument, WIId).Result;
        }

        /// <summary>
        /// Get one work item
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        static WorkItem GetWorkItem(int Id)
        {
            return WitClient.GetWorkItemAsync(Id).Result;
        }

        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
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
