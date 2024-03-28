using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.PermissionsReport.Client;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity.Client;
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
        static PermissionsReportHttpClient PermissionsReportClient;
        static IdentityHttpClient IdentityClient;

        static void Main(string[] args)
        {
            string TeamProject = "<Team Project name>";
            string RepoName = "<Repo Name>";
            string userEmail = "<someuser@mail.com>";
            string reportName = DateTime.Now.Ticks.ToString();
            string filepath = $@"c:\temp\{reportName}.json";

            ConnectWithPAT(TFUrl, UserPAT);

            //Get user indentity
            var user = IdentityClient.ReadIdentitiesAsync(Microsoft.VisualStudio.Services.Identity.IdentitySearchFilter.MailAddress, userEmail).Result.ToArray();

            //Get repo information
            var repo = GitClient.GetRepositoryAsync(TeamProject, RepoName).Result;            

            //Construct a report request
            PermissionsReportResource res = new PermissionsReportResource { ResourceType = ResourceType.Repo, ResourceName = RepoName, ResourceId = repo.Id.ToString() };

            var reportRequest = new PermissionsReportRequest
            {
                Resources = new PermissionsReportResource[] { res },
                ReportName = reportName,
                Descriptors = new string[] { user[0].SubjectDescriptor }
            };

            //Create a new request
            PermissionsReportClient.CreatePermissionsReportAsync(reportRequest).Wait();

            //Wait for report generation
            System.Threading.Thread.Sleep(10000);

            //Get existing reports
            var reportslist = PermissionsReportClient.GetPermissionsReportsAsync().Result;

            var report = (from r in reportslist where r.ReportName == reportName select r).FirstOrDefault();

            if (report == null)
            {
                Console.WriteLine("Can not find the report " + reportName);
                return;
            }

            //Download Report to the local path
            var reportStream = PermissionsReportClient.DownloadAsync(report.Id).Result;
            var fileStream = System.IO.File.Create(filepath);
            reportStream.CopyTo(fileStream);
            fileStream.Close();
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
            PermissionsReportClient = Connection.GetClient<PermissionsReportHttpClient>();
            IdentityClient = Connection.GetClient<IdentityHttpClient>();
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
