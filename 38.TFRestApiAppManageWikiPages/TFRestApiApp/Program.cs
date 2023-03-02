using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.Wiki;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Wiki.WebApi;
using System.Management.Instrumentation;
using Microsoft.TeamFoundation.SourceControl.WebApi.Legacy;
using Microsoft.TeamFoundation.Wiki.WebApi.Contracts;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<your_or>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your_pat>"; //https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static WikiHttpClient WikiClient;

        static void Main(string[] args)
        {
            try
            {
                string ProjectName = "<Team_Project_Name>";
                string WikiName = "<Wiki_Name>";

                ConnectWithPAT(TFUrl, UserPAT);

                var projectWiki = CreateProjectWiki(ProjectName);

                CreateWikiPages(ProjectName, projectWiki);

                MovePages(ProjectName, projectWiki);

                EditPage(ProjectName, projectWiki);

                AddAttachment(ProjectName, projectWiki);

                ViewPages(ProjectName, projectWiki);

                DeletePage(ProjectName, projectWiki);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }


        /// <summary>
        /// Create a project wiki if not exists.
        /// </summary>
        /// <param name="ProjectName"></param>
        static WikiV2 CreateProjectWiki(string ProjectName)
        {
            var wiki = CheckProjectWiki(ProjectName);
            if (wiki != null) return wiki;

            var project = ProjectClient.GetProject(ProjectName).Result;

            WikiCreateParametersV2 newWikiParams = new WikiCreateParametersV2();
            newWikiParams.Type = WikiType.ProjectWiki; 
            newWikiParams.Name = ProjectName + "11.wiki";
            newWikiParams.ProjectId = project.Id;

            wiki = WikiClient.CreateWikiAsync(newWikiParams).Result;

            Console.WriteLine($@"Wiki is created: ${wiki.Name}");

            return wiki;
        }

        /// <summary>
        /// Delete an existing page
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void DeletePage(string ProjectName, WikiV2 wiki)
        {
            WikiClient.DeletePageAsync(ProjectName, wiki.Name, "Page 1/Page 12").Wait();
        }

        /// <summary>
        /// View existing pages
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void ViewPages(string ProjectName, WikiV2 wiki)
        {
            WikiPagesBatchRequest request = new WikiPagesBatchRequest();
            request.Top = 10;

            var pages = WikiClient.GetPagesBatchAsync(request, ProjectName, wiki.Name).Result;

            foreach(var page in pages)
            {
                Console.WriteLine($@"{page.Id} : {page.Path}");
            }
        }

        /// <summary>
        /// Upload the icon and add it to an existing page
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void AddAttachment(string ProjectName, WikiV2 wiki)
        {
            string filename = "icon.png";

            var bytes = File.ReadAllBytes(filename);

            var attachment = WikiClient.CreateAttachmentAsync(new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes))), ProjectName, wiki.Name, filename).Result;

            var wikiPage = WikiClient.GetPageAsync(ProjectName, wiki.Name, "Page 2").Result;

            WikiPageCreateOrUpdateParameters parametersWikiPage = new WikiPageCreateOrUpdateParameters();

            parametersWikiPage.Content = $@"![{attachment.Attachment.Name}]({attachment.Attachment.Path})";

            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 2", wikiPage.ETag.ElementAt(0), "Updated Page 2").Wait();
        }

        /// <summary>
        /// Edit an existing page
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void EditPage(string ProjectName, WikiV2 wiki)
        {
            var wikiPage = WikiClient.GetPageAsync(ProjectName, wiki.Name, "Page 1").Result;

            WikiPageCreateOrUpdateParameters parametersWikiPage = new WikiPageCreateOrUpdateParameters();

            parametersWikiPage.Content = "|Column 1|Column 2 |Column 3 |\r\n|--|--|--|\r\n| Value 1 | Value 2 | Value 3 |\r\n";

            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 1", wikiPage.ETag.ElementAt(0), "Updated Page 1").Wait();
        }

        /// <summary>
        /// Reorder and Reparent pages
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void MovePages(string ProjectName, WikiV2 wiki)
        {
            //reorder pages
            var wikiPage = WikiClient.GetPageAsync(ProjectName, wiki.Name, "Page 1").Result;

            WikiPageMoveParameters wikiPageMoveParameters = new WikiPageMoveParameters();
            wikiPageMoveParameters.NewOrder = wikiPage.Page.Order - 1;
            wikiPageMoveParameters.Path = wikiPage.Page.Path;

            WikiClient.CreatePageMoveAsync(wikiPageMoveParameters, ProjectName, wiki.Name).Wait();

            //reparent pages
            var wikiPageChild = WikiClient.GetPageAsync(ProjectName, wiki.Name, "Page 2/Page 21").Result;

            WikiPageMoveParameters wikiPageChildMoveParameters = new WikiPageMoveParameters();
            wikiPageChildMoveParameters.Path = wikiPageChild.Page.Path;
            wikiPageChildMoveParameters.NewPath = "Page 1/Page 21";
            wikiPageChildMoveParameters.NewOrder = 0;
            WikiClient.CreatePageMoveAsync(wikiPageChildMoveParameters, ProjectName, wiki.Name).Wait();
        }

        /// <summary>
        /// Create sample structure
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="wiki"></param>
        static void CreateWikiPages(string ProjectName, WikiV2 wiki)
        {
            WikiPageCreateOrUpdateParameters parametersWikiPage = new WikiPageCreateOrUpdateParameters();

            parametersWikiPage.Content = "Page 1";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 1", null).Wait();
            parametersWikiPage.Content = "Page 11";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 1/Page 11", null).Wait();
            parametersWikiPage.Content = "Page 12";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 1/Page 12", null).Wait();

            parametersWikiPage.Content = "Page 2";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 2", null).Wait();
            parametersWikiPage.Content = "Page 21";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 2/Page 21", null).Wait();
            parametersWikiPage.Content = "Page 22";
            WikiClient.CreateOrUpdatePageAsync(parametersWikiPage, ProjectName, wiki.Name, "Page 2/Page 22", null).Wait();
        }


        /// <summary>
        /// Get or create project wiki
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <returns></returns>
        static WikiV2 CheckProjectWiki(string ProjectName)
        {
            var wikiLIST = WikiClient.GetAllWikisAsync(ProjectName).Result;

            return (from w in wikiLIST where w.Type == WikiType.ProjectWiki select w).FirstOrDefault();
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
            WikiClient = Connection.GetClient<WikiHttpClient>();
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
