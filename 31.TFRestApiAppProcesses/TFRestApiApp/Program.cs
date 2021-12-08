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

            AddAndEditProcess();

            Console.ReadKey();

            GetProcesses();

            Console.ReadKey();

            RemoveProcess();
        }

        /// <summary>
        /// Remove an existing project
        /// </summary>
        private static void RemoveProcess()
        {
            string processName = "My New Process";

            var processes = ProcessHttpClient.GetListOfProcessesAsync().Result;

            var processToRemoveId = (from p in processes where p.Name == processName select p.TypeId).FirstOrDefault();

            if (processToRemoveId != null)
            {
                ProcessHttpClient.DeleteProcessByIdAsync(processToRemoveId).Wait();

                Console.WriteLine("Done");
            }
            else
                Console.WriteLine("Can not find project to remove");
        }


        /// <summary>
        /// Create a new process and disable it
        /// </summary>
        private static void AddAndEditProcess()
        {
            string processName = "My New Process";
            string processParetName = "Agile";
            string processDescription = "My New Process";
            string processDescriptionUpdated = "My New Updated Process";

            var processes = ProcessHttpClient.GetListOfProcessesAsync().Result;

            var parentProcessId = (from p in processes where p.Name == processParetName select p.TypeId).FirstOrDefault();

            if (parentProcessId != null)
            {
                var newProcess = ProcessHttpClient.CreateNewProcessAsync(new CreateProcessModel() { Name = processName, Description = processDescription, ParentProcessTypeId = parentProcessId }).Result;

                Console.WriteLine("New process: " + newProcess.Name);
                Console.WriteLine("New process Id: " + newProcess.TypeId);

                ProcessHttpClient.EditProcessAsync(new UpdateProcessModel { Description = processDescriptionUpdated, IsEnabled = false }, newProcess.TypeId);
            }
            else
                Console.WriteLine("Can not find parent project");
        }


        /// <summary>
        /// Get all process and their projects
        /// </summary>
        private static void GetProcesses()
        {
            var processes = ProcessHttpClient.GetListOfProcessesAsync(GetProcessExpandLevel.Projects).Result;

            Console.WriteLine("{0, -20} : {1, -36} : {2, -15} : {3, -10} : {4, -7} : {5}", "Process Name", "Process Id", "Process Type", "Parent", "Default", "Enabled");
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");

            foreach (var process in processes)
            {
                var parent = "None";
                if (process.ParentProcessTypeId != Guid.Empty)
                    parent = (from p in processes where p.TypeId == process.ParentProcessTypeId select p.Name).FirstOrDefault();
                Console.WriteLine("{0, -20} : {1} : {2, -15} : {3, -10} : {4, -7} : {5}", process.Name, process.TypeId, process.CustomizationType, parent, process.IsDefault, process.IsEnabled);

                if (process.Projects != null)
                {
                    Console.WriteLine(" Projects:");

                    foreach (var project in process.Projects)
                    {
                        Console.WriteLine("     " + project.Name);
                    }
                }
            }
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
    }
}
