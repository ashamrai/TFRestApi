using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
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
        static readonly string UserPAT = "your pat";

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            ConnectWithDefaultCreds(TFUrl);

            string teamProject = ""; //team prtoject name
            string templateName = ""; //work item template name

            WorkItemTemplate wiTemplate = GetTemplate(teamProject, templateName);

            if (wiTemplate != null)
            {
                Dictionary<string, object> fields = new Dictionary<string, object>();
                fields.Add("System.Title", "New work item");

                var newWorkItem = CreateWorkItemByTemplate(teamProject, wiTemplate, fields);
            }
        }

        /// <summary>
        /// Get template by name from default team
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="templateName"></param>
        /// <returns></returns>
        static WorkItemTemplate GetTemplate(string projectName, string templateName)
        {
            //get project team
            var project = ProjectClient.GetProject(projectName).Result;

            //get context for default project team
            TeamContext tmcntx = new TeamContext(project.Id, project.DefaultTeam.Id);

            //get all templates for team
            var templates = WitClient.GetTemplatesAsync(tmcntx).Result;

            //get tempate through its name
            var id = (from tm in templates where tm.Name == templateName select tm.Id).FirstOrDefault();

            if (id != null) return WitClient.GetTemplateAsync(tmcntx, id).Result;

            return null;
        }

        /// <summary>
        /// Create a new work item based on template
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="template"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        static WorkItem CreateWorkItemByTemplate(string projectName, WorkItemTemplate template, Dictionary<string, object> fields)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var templateKey in template.Fields.Keys) //set default fields from template
                if (!fields.ContainsKey(templateKey)) //exclude fields added by users
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = "/fields/" + templateKey,
                        Value = template.Fields[templateKey]
                    });

            //add user fields
            foreach (var key in fields.Keys) 
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/" + key,
                    Value = fields[key]
                });

            return WitClient.CreateWorkItemAsync(patchDocument, projectName, template.WorkItemTypeName).Result;
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
