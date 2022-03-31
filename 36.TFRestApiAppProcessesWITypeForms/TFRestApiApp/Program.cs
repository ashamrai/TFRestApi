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
        static readonly string AzDOUrl = "https://dev.azure.com/<org>";
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
            string newFieldName = "Microsoft.VSTS.Common.AcceptanceCriteria"; //existing field
            Guid procId;
            string witRefName;

            GetProcAndWIT(processName, witName, out procId, out witRefName);

            AddField(newFieldName, procId, witRefName);
            ShowLayout(procId, witRefName);
            AddNewControl(newFieldName, procId, witRefName);

        }

        /// <summary>
        /// Add new page, group and control. Warning: Currently does not work with non-contribution fields.
        /// </summary>
        /// <param name="newFieldName"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void AddNewControl(string newFieldName, Guid procId, string witRefName)
        {
            Page pageRequest = new Page();
            pageRequest.Label = "Business Value";
            pageRequest.PageType = PageType.Custom;

            var newPage = ProcessHttpClient.AddPageAsync(pageRequest, procId, witRefName).Result;

            Group groupRequest = new Group();
            groupRequest.Label = "Value";
            groupRequest.Controls = new List<Control>();
            groupRequest.Visible = true;

            Control newControl = new Control();
            newControl.Id = newFieldName;
            newControl.Label = "Value";
            newControl.ControlType = "HtmlFieldControl";
            newControl.Visible = true;
            newControl.ReadOnly = false;

            groupRequest.Controls.Add(newControl);

            var newGroup = ProcessHttpClient.AddGroupAsync(groupRequest, procId, witRefName, newPage.Id, newPage.Sections[0].Id).Result;
        }

        /// <summary>
        /// Show form controls
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void ShowLayout(Guid procId, string witRefName)
        {
            var wiForm = ProcessHttpClient.GetFormLayoutAsync(procId, witRefName).Result;

            Console.WriteLine("------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("{0, -20} : {1, -30} : {2, -20} : {3, -8} : {4, -8}",
                "Id", "Type", "Label", "ReadOnly", "Visibale");
            Console.WriteLine("------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("System Controls");
            Console.WriteLine("------------------------------------------------------------------------------------------------------------");

            foreach (var control in wiForm.SystemControls)
            {
                Console.WriteLine("{0, -20} : {1, -30} : {2, -20} : {3, -8} : {4, -8}", control.Id, control.ControlType, control.Label, control.ReadOnly, control.Visible);
            }

            foreach (var page in wiForm.Pages)
            {
                Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("Page " + page.Id + " : " + page.Label);
                Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                foreach (var section in page.Sections)
                {
                    Console.WriteLine("           Section " + section.Id);
                    Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                    foreach (var group in section.Groups)
                    {
                        Console.WriteLine("                     Section " + group.Id + " : " + group.Label);
                        Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                        foreach (var control in group.Controls)
                            Console.WriteLine("{0, -20} : {1, -30} : {2, -20} : {3, -8} : {4, -8}", control.Id, control.ControlType, control.Label, control.ReadOnly, control.Visible);

                        Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                    }

                    Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                }

            }
        }

        /// <summary>
        /// Add existing field to work item type
        /// </summary>
        /// <param name="newFieldName"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void AddField(string newFieldName, Guid procId, string witRefName)
        {
            if (witRefName.StartsWith("Microsoft."))
            {
                witRefName = CreateInheritedWIT(procId, witRefName);
            }

            AddProcessWorkItemTypeFieldRequest newFieldRequest = new AddProcessWorkItemTypeFieldRequest();
            newFieldRequest.ReferenceName = newFieldName;

            ProcessHttpClient.AddFieldToWorkItemTypeAsync(newFieldRequest, procId, witRefName).Wait();
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
        private static void ShowCurrentFields(Guid procId, string witRefName)
        {
            var fields = ProcessHttpClient.GetAllWorkItemTypeFieldsAsync(procId, witRefName).Result;

            Console.WriteLine("{0, -20} : {1, -40} : {2, -10} : {3, -8} : {4, -8} : {5, -8}", 
                "Name", "Reference Name", "Type", "Required", "ReadOnly", "Default");


            foreach (var field in fields)
            {
                Console.WriteLine("------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("{0, -20} : {1, -40} : {2, -10} : {3, -8} : {4, -8} : {5, -8}",
                    field.Name, field.ReferenceName, field.Type, field.Required, field.ReadOnly, field.DefaultValue);

                var fieldDetails = ProcessHttpClient.GetWorkItemTypeFieldAsync(procId, witRefName, field.ReferenceName, expand: ProcessWorkItemTypeFieldsExpandLevel.All).Result;

                if (fieldDetails.AllowedValues != null && fieldDetails.AllowedValues.Length > 0)
                {
                    Console.WriteLine("Allowed values");
                    foreach(string value in fieldDetails.AllowedValues)
                    {
                        Console.WriteLine(value);
                    }
                }                
            }

            Console.WriteLine("------------------------------------------------------------------------------------------------------------\n\n\n\n");
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
    }
}
