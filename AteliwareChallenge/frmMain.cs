using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Octokit;

namespace AteliwareChallenge
{
    public partial class frmMain : Form
    {
        string connectionString;
        string name;
        string login;

        public frmMain()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["AteliwareChallenge.Properties.Settings.GitRepoConnectionString"].ConnectionString;
        }

        private void getRepoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            getResults();
            LoadRecords();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'gitRepoDataSet.SearchResults' table. You can move, or remove it, as needed.
            this.searchResultsTableAdapter.Fill(this.gitRepoDataSet.SearchResults);
            LoadRecords();
        }

        private void getResults()
        {
            var searchClient = new SearchClient(new ApiConnection(new Connection(new ProductHeaderValue("SearchRepo"))));
            var searchRepositoriesRequest = new SearchRepositoriesRequest
            {
                Language = Language.CSharp,
                Forks = Range.GreaterThan(400),
                SortField = RepoSearchSort.Stars,
                Order = SortDirection.Descending
            };

            var searchRepositoryResult = searchClient.SearchRepo(searchRepositoriesRequest).Result.Items.ToList();

            foreach (var result in searchRepositoryResult.Take(5))
            {
                InsertResult(result.Name, result.HtmlUrl, result.Owner.Login);
            }
        }

        private void InsertResult(string _name, string _url, string _login)
        {
            byte[] zipFile = DownloadProjectsFromGitHub(_name, _url);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = " IF NOT EXISTS (SELECT * FROM SearchResults WHERE url = @url) \n\r" +
                " BEGIN \n\r" +
                "     INSERT INTO SearchResults(name, url, repository, login) VALUES (@name, @url, @repository, @login) \n\r" +
                " SELECT SCOPE_IDENTITY() \n\r " +
                " END \n\r " +
                " ELSE SELECT 0";

                using (
                    SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@name", _name);
                    command.Parameters.AddWithValue("@url", _url);
                    command.Parameters.AddWithValue("@login", _login);
                    command.Parameters.AddWithValue("@repository", zipFile);

                    connection.Open();
                    int result = command.ExecuteNonQuery();

                    if (result < 0)
                    {
                        Console.WriteLine("Error inserting data into Database!");
                    }

                }
            }
        }

        private byte[] DownloadProjectsFromGitHub(string _name, string _url)
        {
            Regex illegalInFileName = new Regex(@"[\\/:*?""<>|]");
            string name = illegalInFileName.Replace(_name, "");

            var archivePath = Path.Combine(GetBuildFolder(), name + ".zip");
            var destinationDirectoryName = Path.Combine(GetBuildFolder(), name);
            if (!File.Exists(archivePath))
            {
                var downloadUrl = _url + "/archive/master.zip";
                using (var client = new WebClient())
                {
                    client.DownloadFile(downloadUrl, archivePath);
                }
            }
            byte[] bytes = System.IO.File.ReadAllBytes(archivePath);
            File.Delete(archivePath); ;

            return bytes;
        }

        private void LoadRecords()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM SearchResults", connection))
            {
                connection.Open();

                DataTable searchResultsTable = new DataTable();
                adapter.Fill(searchResultsTable);
                gridSearchResult.DataSource = searchResultsTable;
            }
        }

        private static string GetBuildFolder()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        private void viewRepoDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(name) )
            {
                MessageBox.Show("Please select a repository.");
            }
            else
            {
                frmDetails form = new frmDetails(login, name);
                form.Show();
            }
        }

        private void gridSearchResult_SelectionChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in gridSearchResult.SelectedRows)
            {
                name = row.Cells[0].Value.ToString();
                login = row.Cells[2].Value.ToString();
            }
        }
    }
}
