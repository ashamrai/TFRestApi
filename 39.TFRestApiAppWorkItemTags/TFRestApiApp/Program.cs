using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Common;
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
        static TaggingHttpClient TaggingClient;

        static void Main(string[] args)
        {
            string teamProject = "<TeamProjectName>";
            string newTagName = "new tag 0.1.1";
            string updatedTagName = "new tag 0.2";

            ConnectWithPAT(AzDOUrl, UserPAT);

            AddTag(teamProject, newTagName);

            Console.ReadKey();

            UpdateTag(teamProject, newTagName, updatedTagName);

            Console.ReadKey();

            ViewAllTagsOnOrg();

            Console.ReadKey();

            UpdateTag(teamProject, updatedTagName, null, false);

            Console.ReadKey();

            RemoveTag(teamProject, newTagName);

            Console.ReadKey();
        }

        /// <summary>
        /// Add new tag to project
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="newTagName"></param>
        /// <exception cref="Exception"></exception>
        private static void AddTag(string teamProject, string newTagName)
        {
            var prjId = ProjectClient.GetProject(teamProject).Result.Id;            

            try
            {
                var existingTag = TaggingClient.GetTagAsync(prjId, newTagName).Result;

                Console.WriteLine($@"Tag exists: {existingTag.Id} - {existingTag.Name}");

                return;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == null || ex.InnerException.GetType().Name != "TagNotFoundException")
                {
                   throw new Exception("Unknow exception", ex);
                }
            }

            var newTag = TaggingClient.CreateTagAsync(prjId, newTagName).Result;

            Console.WriteLine($@"Tag created: {newTag.Id} - {newTag.Name}");
        }

        /// <summary>
        /// Rename or dactivate tag
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="tagOldName"></param>
        /// <param name="tagNewName"></param>
        /// <param name="tagActive"></param>
        private static void UpdateTag(string teamProject, string tagOldName, string tagNewName, bool tagActive = true)
        {
            var prjId = ProjectClient.GetProject(teamProject).Result.Id;

            var tag = (from t in TaggingClient.GetTagsAsync(prjId).Result where t.Name == tagOldName select t).FirstOrDefault();

            if (tag == null) 
            {
                Console.WriteLine("Tag does not exist: " + tagOldName);
                return;
            }

            var newTag =  (tagNewName.IsNullOrEmpty()) ? 
                TaggingClient.UpdateTagAsync(prjId, tag.Id, tag.Name, tagActive).Result:
                TaggingClient.UpdateTagAsync(prjId, tag.Id, tagNewName, tagActive).Result;

            Console.WriteLine($@"Tag is updated: {newTag.Id} - {newTag.Name} - {newTag.Active.Value}");
        }

        /// <summary>
        /// View all tags in each project
        /// </summary>
        private static void ViewAllTagsOnOrg()
        {
            var projects = ProjectClient.GetProjects().Result;

            foreach (var project in projects)
            {
                Console.WriteLine(project.Name);

                var tags = TaggingClient.GetTagsAsync(project.Id, true).Result;

                foreach (var tag in tags)
                    Console.WriteLine($@"     {tag.Id} - {tag.Name} - {tag.Active.Value}");
            }
        }

        /// <summary>
        /// Remove tag
        /// </summary>
        /// <param name="teamProject"></param>
        /// <param name="tagName"></param>
        private static void RemoveTag(string teamProject, string tagName)
        {
            var prjId = ProjectClient.GetProject(teamProject).Result.Id;

            var tag = (from t in TaggingClient.GetTagsAsync(prjId).Result where t.Name == tagName select t).FirstOrDefault();

            if (tag == null)
            {
                Console.WriteLine("Tag does not exist: " + tagName);
                return;
            }

            TaggingClient.DeleteTagAsync(prjId, tag.Id).Wait();

            Console.WriteLine(tagName + " was removed");
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
            TaggingClient = Connection.GetClient<TaggingHttpClient>();
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
