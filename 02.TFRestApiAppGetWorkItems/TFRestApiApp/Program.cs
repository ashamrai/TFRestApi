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
        static readonly string TFUrl = "https://dev.azure.com/<your_org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your_pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

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
                int wiId = -1; //set the work item ID
                ConnectWithPAT(TFUrl, UserPAT);
                var wi = GetWorkItemWithRelations(wiId);
                var fieldValue = CheckFieldAndGetFieldValue(wi, "System.Title"); // or just: var fieldValue = GetFieldValue(wi, "System.Title");

                Console.WriteLine("__________________________________________");
                Console.WriteLine("                   FIELDS");
                Console.WriteLine("__________________________________________");

                foreach (var fieldName in wi.Fields.Keys)
                    Console.WriteLine("{0,-40}: {1}", fieldName, wi.Fields[fieldName].ToString());

                if (wi.Relations != null)
                {
                    Console.WriteLine("__________________________________________");
                    Console.WriteLine("                   LINKS");
                    Console.WriteLine("__________________________________________");

                    foreach (var wiLink in wi.Relations)
                        Console.WriteLine("{0,-40}: {1}", wiLink.Rel, ExtractWiIdFromUrl(wiLink.Url));
                }

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
        /// Extract an id from an url
        /// </summary>
        /// <param name="Url"></param>
        /// <returns></returns>
        static int ExtractWiIdFromUrl(string Url)
        {
            int id = -1;

            string splitStr = "_apis/wit/workItems/";

            if (Url.Contains(splitStr))
            {
                string [] strarr = Url.Split(new string[] { splitStr }, StringSplitOptions.RemoveEmptyEntries);

                if (strarr.Length == 2 && int.TryParse(strarr[1], out id))
                    return id;
            }

            return id;
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
