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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static ProcessHttpClient ProcessClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            string newTeamProjectName = "My New Project";
            string updatedTeamProjectName = "My Renamed Project";

            ConnectWithPAT(TFUrl, UserPAT);

            CreateTeamProject(newTeamProjectName);

            RenameProject(newTeamProjectName, updatedTeamProjectName);

            Console.ReadKey();

            ViewTeamProjects();

            Console.ReadKey();

            DeleteTeamProject(updatedTeamProjectName);

            Console.ReadKey();

            ViewTeamProjects(true);

            Console.ReadKey();

            RestoreTeamProject(updatedTeamProjectName);

        }

        /// <summary>
        /// View active or deleted projects
        /// </summary>
        /// <param name="showDeleted"></param>
        static void ViewTeamProjects(bool showDeleted = false)
        {
            var teamProjects = (showDeleted) ? 
                ProjectClient.GetProjects(ProjectState.Deleted).Result : 
                ProjectClient.GetProjects().Result;

            foreach(var project in teamProjects)
            {
                Console.WriteLine($@"{project.Id} - {project.Name} - {project.State}");
                Console.WriteLine("Description:\n" + project.Description);
            }
        }

        /// <summary>
        /// Create new project based on Agile prcess template
        /// </summary>
        /// <param name="projectName"></param>
        static void CreateTeamProject(string projectName)
        {
            TeamProject project = new TeamProject();
            project.Name = projectName;
            project.Description = "Created through API";
            project.Capabilities = new Dictionary<string, Dictionary<string, string>>();

            //add process info
            var processId = (from p in ProcessClient.GetProcessesAsync(project).Result
                             where p.Name == "Agile"
                             select p.Id).FirstOrDefault();

            if (processId == null)
            {
                Console.WriteLine("Can not find process");
                return;
            }

            project.Capabilities[TeamProjectCapabilitiesConstants.VersionControlCapabilityName] =
                new Dictionary<string, string> { 
                    [TeamProjectCapabilitiesConstants.VersionControlCapabilityAttributeName] = SourceControlTypes.Git.ToString() 
                };
            project.Capabilities[TeamProjectCapabilitiesConstants.ProcessTemplateCapabilityName] =
                new Dictionary<string, string>
                {
                    [TeamProjectCapabilitiesConstants.ProcessTemplateCapabilityTemplateTypeIdAttributeName] = processId.ToString()
                };

            ProjectClient.QueueCreateProject(project).Wait();
        }

        /// <summary>
        /// Rename team project
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="newProjectName"></param>
        static void RenameProject(string projectName, string newProjectName)
        {
            var project = (from p in ProjectClient.GetProjects().Result
                             where p.Name == projectName
                             select p).FirstOrDefault();
            if (project != null)
            {
                TeamProject updateProject = new TeamProject();
                updateProject.Name = newProjectName;
                updateProject.Description = newProjectName;

                var updatedProject = ProjectClient.UpdateProject(project.Id, updateProject).Result;
            }
        }

        /// <summary>
        /// Update project state to WellFormed
        /// </summary>
        /// <param name="projectName"></param>
        static void RestoreTeamProject(string projectName)
        {
            TeamProject project = new TeamProject();
            project.State = ProjectState.WellFormed;

            var projectId = (from p in ProjectClient.GetProjects(ProjectState.Deleted).Result
                             where p.Name == projectName 
                             select p.Id).FirstOrDefault();

            if (projectId != null && projectId != Guid.Empty)
                ProjectClient.UpdateProject(projectId, project);
        }

        /// <summary>
        /// Delete team project
        /// </summary>
        /// <param name="projectName"></param>
        static void DeleteTeamProject(string projectName)
        {
            var projectId = (from p in ProjectClient.GetProjects().Result
                                             where p.Name == projectName
                                             select p.Id).FirstOrDefault();
            if (projectId != null)
                ProjectClient.QueueDeleteProject(projectId);
        }


        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            ProcessClient = Connection.GetClient<ProcessHttpClient>();
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
