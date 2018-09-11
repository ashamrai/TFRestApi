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
        static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; // for tfs
        //static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for vsts
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "personal access token";

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
                ConnectWithDefaultCreds(TFUrl); //ConnectWithPAT(TFUrl, UserPAT);
                int bugId = CreateNewBug();
                EditBug(bugId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        static int CreateNewBug()
        {
            Dictionary<string, object> fields = new Dictionary<string, object>();

            fields.Add("Title", "Bug from app");
            fields.Add("Repro Steps", "<ol><li>Run app</li><li>Crash</li></ol>");
            fields.Add("Priority", 1);

            var newBug = CreateWorkItem("TFSAgile", "Bug", fields);

            return newBug.Id.Value;
        }
        
        static int EditBug(int WIId)
        {
            Dictionary<string, object> fields = new Dictionary<string, object>();

            fields.Add("Title", "Bug from app updated");
            fields.Add("Repro Steps", "<ol><li>Run app</li><li>Crash</li><li>Updated step</li></ol>");
            fields.Add("History", "Comment from app");

            var editedBug = UpdateWorkItem(WIId, fields); //var editedBug = UpdateWorkItemAndCheckRev(WIId, fields);


            return editedBug.Id.Value;
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
        /// Update a work item and check revison before update
        /// </summary>
        /// <param name="WIId"></param>
        /// <param name="Fields"></param>
        /// <returns></returns>
        static WorkItem UpdateWorkItemAndCheckRev(int WIId, Dictionary<string, object> Fields)
        {
            WorkItem bug = GetWorkItem(WIId);

            JsonPatchDocument patchDocument = new JsonPatchDocument();
            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Test,
                    Path = "/rev",
                    Value = bug.Rev
                });

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
        /// Create a work item
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WorkItemTypeName"></param>
        /// <param name="Fields"></param>
        /// <returns></returns>
        static WorkItem CreateWorkItem(string ProjectName, string WorkItemTypeName, Dictionary<string, object> Fields)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var key in Fields.Keys)
                patchDocument.Add(new JsonPatchOperation() {
                    Operation = Operation.Add,
                    Path = "/fields/" + key,
                    Value = Fields[key]
                });

            return WitClient.CreateWorkItemAsync(patchDocument, ProjectName, WorkItemTypeName).Result;
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

        #region connection

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
