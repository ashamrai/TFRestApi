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
        static readonly string AzDOUrl = "https://dev.azure.com/<arg>/";
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

            var procId = AddProcess("Agile", "My New Process", "My New Process");

            ViewWorkItemTypes(procId);

            CreateAndDiableWorkItemType(procId);

            ReCreateIssueWIT(procId);

        }

        /// <summary>
        /// Create and remove custom work item type based on a defult type
        /// </summary>
        /// <param name="procId"></param>
        static private void ReCreateIssueWIT(Guid procId)
        {
            var issueRef = "Microsoft.VSTS.WorkItemTypes.Issue";

            CreateProcessWorkItemTypeRequest cpwit = new CreateProcessWorkItemTypeRequest();
            cpwit.Icon = wi_icons.clipboard_issue;
            cpwit.Color = "f6546a";
            cpwit.Description = "My new work item type to track issues";
            cpwit.InheritsFrom = issueRef;

            var newwit = ProcessHttpClient.CreateProcessWorkItemTypeAsync(cpwit, procId).Result;

            Console.WriteLine("Updated work item type: {0} - {1}", newwit.Name, newwit.ReferenceName);

            Console.ReadKey();

            UpdateProcessWorkItemTypeRequest upwit = new UpdateProcessWorkItemTypeRequest();
            upwit.Icon = wi_icons.car;

            var iswit = ProcessHttpClient.UpdateProcessWorkItemTypeAsync(upwit, procId, newwit.ReferenceName).Result;

            Console.WriteLine("Updated work item type: {0} - {1}", iswit.Name, iswit.ReferenceName);

            Console.ReadKey();

            ProcessHttpClient.DeleteProcessWorkItemTypeAsync(procId, iswit.ReferenceName).Wait();

            Console.WriteLine("Work item type removed");

            Console.ReadKey();
        }

        /// <summary>
        /// Create and disable new custom work item type
        /// </summary>
        /// <param name="procId"></param>
        private static void CreateAndDiableWorkItemType(Guid procId)
        {
            CreateProcessWorkItemTypeRequest cpwit = new CreateProcessWorkItemTypeRequest();
            cpwit.Name = "Child Task";
            cpwit.Icon = wi_icons.asterisk;
            cpwit.Color = "f6546a";
            cpwit.Description = "My new work item type to track child work";

            var newwit = ProcessHttpClient.CreateProcessWorkItemTypeAsync(cpwit, procId).Result;

            Console.WriteLine("New work item type: {0} - {1}", newwit.Name, newwit.ReferenceName);

            Console.ReadKey();

            UpdateProcessWorkItemTypeRequest upwit = new UpdateProcessWorkItemTypeRequest();
            upwit.Description = "Temporary disabled";
            upwit.IsDisabled = true;
            upwit.Icon = wi_icons.broken_lightbulb;

            newwit = ProcessHttpClient.UpdateProcessWorkItemTypeAsync(upwit, procId, newwit.ReferenceName).Result;

            Console.WriteLine("Disabled work item type: {0} - {1}", newwit.Name, newwit.ReferenceName);

            Console.ReadKey();
        }

        /// <summary>
        /// Vioew all work item types
        /// </summary>
        /// <param name="procId"></param>
        private static void ViewWorkItemTypes(Guid procId)
        {
            var workItemTypes = ProcessHttpClient.GetProcessWorkItemTypesAsync(procId, GetWorkItemTypeExpand.None).Result;

            Console.WriteLine("{0, -20} : {1, -40} : {2, -10}", "Work Item Type", "Reference", "Disabled");

            foreach (var workItemType in workItemTypes)
            {
                Console.WriteLine("--------------------------------------");
                Console.WriteLine("{0, -20} : {1, -40} : {2, -10}\n{3}", workItemType.Name, workItemType.ReferenceName, workItemType.IsDisabled, workItemType.Description);
            }
        }

        /// <summary>
        /// Create a new process and disable it
        /// </summary>
        private static Guid AddProcess(string processParetName, string processName, string processDescription)
        {
            Guid newProcessGuid = Guid.Empty;
            
            var processes = ProcessHttpClient.GetListOfProcessesAsync().Result;

            var parentProcessId = (from p in processes where p.Name == processParetName select p.TypeId).FirstOrDefault();

            if (parentProcessId != null)
            {
                var newProcess = ProcessHttpClient.CreateNewProcessAsync(new CreateProcessModel() { Name = processName, Description = processDescription, ParentProcessTypeId = parentProcessId }).Result;

                newProcessGuid = newProcess.TypeId;
            }

            return newProcessGuid;
        }

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


        public static class wi_icons
        {
            public const string crown = "icon_crown";
            public const string trophy = "icon_trophy";
            public const string list = "icon_list";
            public const string book = "icon_book";
            public const string sticky_note = "icon_sticky_note";
            public const string clipboard = "icon_clipboard";
            public const string insect = "icon_insect";
            public const string traffic_cone = "icon_traffic_cone";
            public const string chat_buble = "icon_chat_buble";
            public const string flame = "icon_flame";
            public const string megaphone = "icon_megaphone";
            public const string test_plan = "icon_test_plan";
            public const string test_suite = "icon_test_suite";
            public const string test_case = "icon_test_case";
            public const string test_step = "icon_test_step";
            public const string test_parameter = "icon_test_parameter";
            public const string code_review = "icon_code_review";
            public const string code_response = "icon_code_response";
            public const string review = "icon_review";
            public const string response = "icon_response";
            public const string ribbon = "icon_ribbon";
            public const string chart = "icon_chart";
            public const string headphone = "icon_headphone";
            public const string key = "icon_key";
            public const string airplane = "icon_airplane";
            public const string car = "icon_car";
            public const string diamond = "icon_diamond";
            public const string asterisk = "icon_asterisk";
            public const string database_storage = "icon_database_storage";
            public const string goverment = "icon_goverment";
            public const string gavel = "icon_gavel";
            public const string parachute = "icon_parachute";
            public const string paint_brush = "icon_paint_brush";
            public const string palette = "icon_palette";
            public const string gear = "icon_gear";
            public const string check_box = "icon_check_box";
            public const string gift = "icon_gift";
            public const string test_beaker = "icon_test_beaker";
            public const string broken_lightbulb = "icon_broken_lightbulb";
            public const string clipboard_issue = "icon_clipboard_issue";
            public const string github = "icon_github";
            public const string pull_request = "icon_pull_request";
            public const string github_issue = "icon_github_issue";
        }
    }
}
