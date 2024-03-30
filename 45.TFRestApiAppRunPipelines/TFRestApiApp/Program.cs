using Microsoft.Azure.Pipelines.WebApi;
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
        static readonly string TFUrl = "https://dev.azure.com/<org>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static PipelinesHttpClient PipelinesClient;

        static void Main(string[] args)
        {
            string teamProjectName = "<Team Project Name>";
            string pipelineName = "<Pipeline Name>"; 

            ConnectWithPAT(TFUrl, UserPAT);
            int pipelineId = GetPiplineId(teamProjectName, pipelineName);

            if (pipelineId < 0)
            {
                Console.WriteLine("Can not find the pipeline: " + pipelineName);
                return;
            }

            int pipelineRun = RunPipline(teamProjectName, pipelineId);
            ViewLog(teamProjectName, pipelineId, pipelineRun);
        }

        /// <summary>
        /// View log of one task
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="pipelineId"></param>
        /// <param name="pipelineRun"></param>
        private static void ViewLog(string teamProjectName, int pipelineId, int pipelineRun)
        {
            var logCollection = PipelinesClient.ListLogsAsync(teamProjectName, pipelineId, pipelineRun).Result;

            foreach (var log in logCollection.Logs)
            {
                if (log.Id == 6)
                {
                    var detailedLog = PipelinesClient.GetLogAsync(teamProjectName, pipelineId, pipelineRun, log.Id, GetLogExpandOptions.SignedContent).Result;

                    Console.WriteLine("Retriving Logs");

                    var webRequest = WebRequest.Create(detailedLog.SignedContent.Url);

                    using (var response = webRequest.GetResponse())
                    using (var content = response.GetResponseStream())
                    using (var reader = new System.IO.StreamReader(content))
                    {
                        var logContent = reader.ReadToEnd();
                        Console.WriteLine(logContent);
                    }
                }

            }
        }

        /// <summary>
        /// Run pipeline
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="pipelineId"></param>
        /// <returns></returns>
        private static int RunPipline(string teamProjectName, int pipelineId)
        {
            var pipeline = PipelinesClient.GetPipelineAsync(teamProjectName, pipelineId).Result;
            YamlConfiguration pcfg = (YamlConfiguration)pipeline.Configuration;
            RunPipelineParameters rp = new RunPipelineParameters();
            rp.Variables.Add("var1", pcfg.Variables["var1"]);
            rp.Variables["var1"].Value = "Value from rest api";

            var runp = PipelinesClient.RunPipelineAsync(rp, teamProjectName, pipelineId).Result;

            Console.WriteLine("Pipeline is starting");

            int queryRunCount = 0;

            do
            {
                var runpr = PipelinesClient.GetRunAsync(teamProjectName, pipelineId, runp.Id).Result;
                Console.WriteLine("Current state: " + runpr.State);
                if (runpr.State.ToString() == "Completed") break;
                queryRunCount++;
                System.Threading.Thread.Sleep(1000);
            } while (queryRunCount < 60);

            return runp.Id;
        }

        /// <summary>
        /// Get pipeline by name from the root folder
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="pipelineName"></param>
        /// <returns></returns>
        private static int GetPiplineId(string teamProjectName, string pipelineName)
        {
            var pipelines = PipelinesClient.ListPipelinesAsync(teamProjectName).Result;

            var pipeline = (from p in pipelines where p.Name == pipelineName && p.Folder == "\\" select p).FirstOrDefault();

            if (pipeline != null)
                return pipeline.Id;

            return -1;
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
            PipelinesClient = Connection.GetClient<PipelinesHttpClient>();
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
