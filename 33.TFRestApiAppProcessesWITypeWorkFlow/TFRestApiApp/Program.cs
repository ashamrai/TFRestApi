using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
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
        static readonly string AzDOUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static WorkItemTrackingProcessHttpClient ProcessHttpClient;

        static void Main(string[] args)
        {
            ConnectWithPAT(AzDOUrl, UserPAT);
            string processName = "My New Process"; //existing process
            string witName = "Task"; //existing work item type
            Guid procId;
            string witRefName;

            GetProcAndWIT(processName, witName, out procId, out witRefName);

            ShowCurrentStates(procId, witRefName);

            witRefName = CreateNewState(procId, witRefName, "Work In Progress", StateCategies.InProgress);
            CreateNewState(procId, witRefName, "Work Done", StateCategies.InProgress);

            MoveUpState(procId, witRefName, "Work In Progress");
            RemoveState(procId, witRefName, "Active");

            ShowCurrentStates(procId, witRefName);
        }

        /// <summary>
        /// Create a new custom state
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        /// <param name="StateName"></param>
        /// <param name="StateCategory"></param>
        /// <returns></returns>
        private static string CreateNewState(Guid procId, string witRefName, string StateName, string StateCategory)
        {
            if (witRefName.StartsWith("Microsoft."))
            {
                witRefName = CreateInheritedWIT(procId, witRefName);
            }

            WorkItemStateInputModel workItemState = new WorkItemStateInputModel();
            workItemState.Name = StateName;
            workItemState.StateCategory = StateCategory;
            workItemState.Color = "6a4c46";

            ProcessHttpClient.CreateStateDefinitionAsync(workItemState, procId, witRefName).Wait();
            return witRefName;
        }

        /// <summary>
        /// Move up a state in the work item state list
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        /// <param name="StateName"></param>
        private static void MoveUpState(Guid procId, string witRefName, string StateName)
        {
            var states = ProcessHttpClient.GetStateDefinitionsAsync(procId, witRefName).Result;

            var state = (from p in states where p.Name == StateName select p).FirstOrDefault();

            if (state == null)
            {
                throw new Exception("Can not find state " + StateName);
            }

            WorkItemStateInputModel workItemState = new WorkItemStateInputModel();
            workItemState.Order = state.Order - 1;

            ProcessHttpClient.UpdateStateDefinitionAsync(workItemState, procId, witRefName, state.Id).Wait();
        }

        /// <summary>
        /// Remove or hide a state
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        /// <param name="StateName"></param>
        private static void RemoveState(Guid procId, string witRefName, string StateName)
        {
            var states = ProcessHttpClient.GetStateDefinitionsAsync(procId, witRefName).Result;

            var state = (from p in states where p.Name == StateName select p).FirstOrDefault();

            if (state == null)
            {
                throw new Exception("Can not find state " + StateName);
            }

            if (state.CustomizationType != CustomizationType.Custom)
            {
                HideStateModel hideState = new HideStateModel();

                hideState.Hidden = true;

                ProcessHttpClient.HideStateDefinitionAsync(hideState, procId, witRefName, state.Id).Wait();
            }
            else
            {
                ProcessHttpClient.DeleteStateDefinitionAsync(procId, witRefName, state.Id).Wait();
            }
        }


        /// <summary>
        /// Create a inherited work item type to make changes in the work flow
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="parentRef"></param>
        /// <returns></returns>

        static private string CreateInheritedWIT(Guid procId, string parentRef)
        {
            var witDef = ProcessHttpClient.GetProcessWorkItemTypeAsync(procId, parentRef).Result;
            CreateProcessWorkItemTypeRequest cpwit = new CreateProcessWorkItemTypeRequest();
            cpwit.Color = witDef.Color;
            cpwit.Icon = witDef.Icon;
            cpwit.InheritsFrom = parentRef;

            var newwit = ProcessHttpClient.CreateProcessWorkItemTypeAsync(cpwit, procId).Result;

            return newwit.ReferenceName;
        }

        /// <summary>
        /// View currents states and their properties
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void ShowCurrentStates(Guid procId, string witRefName)
        {
            var states = ProcessHttpClient.GetStateDefinitionsAsync(procId, witRefName).Result;

            Console.WriteLine("{0, -10} : {1, -10} : {2, -6} : {3, -8}", "Name", "Category", "Order", "Hidden");


            foreach (var state in states)
            {
                Console.WriteLine("--------------------------------------");
                Console.WriteLine("{0, -10} : {1, -10} : {2, -6} : {3, -8}", state.Name, state.StateCategory, state.Order, state.Hidden);
            }

            Console.WriteLine("--------------------------------------\n\n\n\n");
        }

        /// <summary>
        /// Get process id and work item type reference name by they names
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="witName"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void GetProcAndWIT(string processName, string witName, out Guid procId, out string witRefName)
        {
            procId = GetProcessGuid(processName);
            if (procId == null)
            {
                throw new Exception("Can not find process.");
            }

            witRefName = GetWITrefName(procId, witName);
            if (string.IsNullOrEmpty(witRefName))
            {
                throw new Exception("Can not find work item type.");
            }
        }

        private static Guid GetProcessGuid(string processName)
        {
            Guid newProcessGuid = Guid.Empty;

            var processes = ProcessHttpClient.GetListOfProcessesAsync().Result;

            return (from p in processes where p.Name == processName select p.TypeId).FirstOrDefault();
        }

        private static string GetWITrefName(Guid procGuid, string witName)
        {
            var wiTypes = ProcessHttpClient.GetProcessWorkItemTypesAsync(procGuid).Result;

            return (from p in wiTypes where p.Name == witName select p.ReferenceName).FirstOrDefault();
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
            ProcessHttpClient = Connection.GetClient<WorkItemTrackingProcessHttpClient>();
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

        public static class StateCategies
        {
            public const string Proposed = "Proposed";
            public const string InProgress = "InProgress";
            public const string Completed = "Completed";
            public const string Closed = "Closed";
            public const string Removed = "Removed";
        }
    }
}
