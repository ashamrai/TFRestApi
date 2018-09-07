using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
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
        static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "";

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
                int wiId = 738; //set the work item ID
                ConnectWithDefaultCreds(TFUrl);
                var wi = GetWorkItem(wiId);
                var fieldValue = CheckFieldAndGetFieldValue(wi, "System.Title"); // or just: var fieldValue = GetFieldValue(wi, "System.Title");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
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

        /// <summary>
        /// Get several work items
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
        static List<WorkItem> GetWorkItems(List<int> Ids)
        {
            return WitClient.GetWorkItemsAsync(Ids).Result;
        }

        /// <summary>
        /// Get one work item with information about linked work items
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        static WorkItem GetWorkItemWithRelations(int Id)
        {            
            return WitClient.GetWorkItemAsync(Id, expand: WorkItemExpand.Relations).Result;
        }

        /// <summary>
        /// Get a string value of a field
        /// </summary>
        /// <param name="WI"></param>
        /// <param name="FieldName"></param>
        /// <returns></returns>
        static string GetFieldValue(WorkItem WI, string FieldName)
        {
            if (!WI.Fields.Keys.Contains(FieldName)) return null;

            return (string)WI.Fields[FieldName];
        }

        /// <summary>
        /// Check field in a work item type then get a string field value
        /// </summary>
        /// <param name="WI"></param>
        /// <param name="FieldName"></param>
        /// <returns></returns>
        static string CheckFieldAndGetFieldValue(WorkItem WI, string FieldName)
        {
            WorkItemType wiType = GetWorkItemType(WI);

            var fields = from field in wiType.Fields where field.Name == FieldName || field.ReferenceName == FieldName select field;

            if (fields.Count() < 1) throw new ArgumentException("Work Item Type " + wiType.Name + " does not contain the field " + FieldName, "CheckFieldAndGetFieldValue");

            return GetFieldValue(WI, FieldName);
        }        

        /// <summary>
        /// Get a work item type definition for existing work item
        /// </summary>
        /// <param name="WI"></param>
        /// <returns></returns>
        static WorkItemType GetWorkItemType(WorkItem WI)
        {
            if (!WI.Fields.Keys.Contains("System.WorkItemType")) throw new ArgumentException("There is no WorkItemType field in the workitem", "GetWorkItemType");
            if (!WI.Fields.Keys.Contains("System.TeamProject")) throw new ArgumentException("There is no TeamProject field in the workitem", "GetWorkItemType");

            return WitClient.GetWorkItemTypeAsync((string)WI.Fields["System.TeamProject"], (string)WI.Fields["System.WorkItemType"]).Result;
        }

        /// <summary>
        /// Get a work item type definition from a project
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="WITypeName"></param>
        /// <returns></returns>
        static WorkItemType GetWorkItemType(string TeamProjectName, string WITypeName)
        {
            return WitClient.GetWorkItemTypeAsync(TeamProjectName, WITypeName).Result;
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
