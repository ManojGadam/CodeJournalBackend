using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using PersonalWebsite.Models;
using PersonalWebsite.Repository;
using PersonalWebsite.Utils;
using System;
using System.Net;
using System.Text;
using static System.Net.WebRequestMethods;
using Cookie = System.Net.Cookie;
using System.Net.Http.Headers;

namespace PersonalWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProblemController : Controller
    {
        private readonly HttpClient _httpClient;
        private IProblemRepository _problemRepository;
       // private const string leetCodeApiURL = "https://leetcode.com/graphql";
        public ProblemController(IProblemRepository problemRepository, HttpClient httpClient)
        {
            _problemRepository = problemRepository;
            _httpClient = httpClient;
        }
        [HttpPost("AddProblem")]
        public async Task<IActionResult> AddProblem([FromBody] Problem problem)
        {
           Boolean res = await _problemRepository.SaveProblem(problem);
            if (!res) throw new Exception("Id not found");
            return Ok(new {isSuccessfull = true});
        }

        [HttpGet("GetProblems")]
        public async Task<IActionResult> GetProblems()
        {
           var problems = await _problemRepository.GetProblems();
            //fix this
            return Ok(problems);
        }

        //Split into three apis
        [HttpPost("SaveProblem")]
        public async Task<IActionResult> SaveProblem([FromBody] String url)
        {
            int i = 30;
            StringBuilder title = new();
            while (!url[i].Equals('/'))
            {
                title.Append(url[i]);
                i++;
            }
            String titleSlug = title.ToString();
            var variables = new { titleSlug };
            var responseContent = await HandleGraphql("saveProblem", variables);
            var responseData = JObject.Parse(responseContent);
            var data = responseData["data"];
            if (data ==null || !data.HasValues)
            {
                throw new Exception("Output object is empty");
            }
            var question = data["question"];
            if (question == null || !question.HasValues)
            {
                throw new Exception("Output object is empty");
            }
            var tags = await GetTags(titleSlug);
            Problem problem = new() { Name = (string)question["title"], Difficulty = (string)question["difficulty"], Link = url, TitleSlug = (string)question["titleSlug"] , Tags = String.Join(",",tags.Select(x=>x.name).ToList()),ProblemNumber = (long)question["questionFrontendId"] };
            await _problemRepository.SaveProblem(problem);
            return Ok(new {isSuccessful = true});
        }

        [HttpPost("SaveConfiguration")]
        public async Task<IActionResult> SaveConfiguration([FromBody] Config config)
        {
            await _problemRepository.SaveConfiguration(config);
            return Ok(new { isSuccessful = true });
        }

        private async Task<Config> GetConfiguration()
        {
            return await _problemRepository.GetConfiguration();
        }

        private async Task<List<topicTag>> GetTags(String titleSlug)
        {
            var variables = new { titleSlug };
            var responseData = await HandleGraphql("getTags",variables);
            var leetCodeResponse = JsonConvert.DeserializeObject<GraphQLResponse>(responseData);

            if (leetCodeResponse?.Data?.Question?.TopicTags == null)
            {
                throw new Exception("Output object is empty");
            }

            return leetCodeResponse.Data.Question.TopicTags;
            //return Ok(topicTags);
        }
        
        // Delete cookie before pushing to github. Using cookie to handle the authecation need to update it
        private async Task<submissions> GetSubmissionId(string questionSlug)
        {
            var config = await GetConfiguration();
            _httpClient.DefaultRequestHeaders.Add("Cookie", config.LeetToken);
            var variables = new
            {
                questionSlug,
                offset = 0,
                limit = 20,
                lastKey = (string?)null
            };
            var responseContent = HandleGraphql("getSubmissionId", variables).GetAwaiter().GetResult();
            var leetCodeResponse = JsonConvert.DeserializeObject<SubmissionResponse>(responseContent);
            var response = leetCodeResponse.data.questionSubmissionList.Submissions.FirstOrDefault(x => x.status == 10) ?? throw new Exception("No accepted submission");
            var code = GetCode(response.Id).GetAwaiter().GetResult();
            response.codeDetails = code;
            return response;
        }
        private async Task<CodeDetails> GetCode(long id)
        {
            //USe this to get runtime and memory
            var config = await GetConfiguration();
            _httpClient.DefaultRequestHeaders.Add("Cookie", config.LeetToken);
            var variables = new { submissionId = id };
            var responseContent = await HandleGraphql("getCode", variables);
            var responseData = JsonConvert.DeserializeObject<CodeDetails>(responseContent);
            return responseData;
        }
        private async Task<string> HandleGraphql(string queryName,object variables)
        {
            var leetcodeApiUrl = "https://leetcode.com/graphql";
            var graphqlRequest = new
            {
                query = QueryDictionary.GetQuery(queryName),
                variables
            };
            var httpResponse = _httpClient.PostAsJsonAsync(leetcodeApiUrl, graphqlRequest, CancellationToken.None).GetAwaiter().GetResult();
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new Exception("Api failed");
            }
            var responseContent = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return responseContent;
        }
        [HttpPost("PushToGit")]
        public async Task<IActionResult> PushToGit([FromBody]GitContent content)
        {
            //check this
            var config = await GetConfiguration();
            var RepoName = config.GitURL;
            var response = await GetSubmissionId(content.QuestionSlug);
            var Token = config.GitToken;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalWebsite");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            var userName = RepoName.Split('/')[3];
            var Repo = RepoName.Split('/')[4];
            var commitMessage = $"[Time Beats: {response.codeDetails.data.submissionDetails.runTimePercentile}%] [Memory Beats: {response.codeDetails.data.submissionDetails.memoryPercentile}%] [Solved on: {DateTime.Now:D}]";
            var title = $"{content.QuestionNumber}-{content.QuestionSlug}{FileExtensionDictionary.GetExtension(response.langName)}";
            var putUrl = $"https://api.github.com/repos/{userName}/{Repo}/contents/{title}";
            var sha = GetSha(putUrl).GetAwaiter().GetResult();
            var payload = new
            {
                message = commitMessage,
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(response.codeDetails.data.submissionDetails.code)),
                branch = "main",
                sha
            };
            var jsonPayload = JsonConvert.SerializeObject(payload);
            var putResponse = _httpClient.PutAsync(putUrl, new StringContent(jsonPayload, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();

            if (putResponse.IsSuccessStatusCode)
            {
                //Console.WriteLine("Code pushed successfully!");
                return Ok(new { isSuccessful = true });
            }
            else
            {
                //Console.WriteLine("Failed to push code:");
                var errorContent = await putResponse.Content.ReadAsStringAsync();
                throw new Exception(errorContent);
            }
        }
        private async Task<string?> GetSha(string url)
        {
            var getResponse = await _httpClient.GetAsync(url);
            var fileInfo = await getResponse.Content.ReadAsStringAsync();
            var fileJson = JsonConvert.DeserializeObject<dynamic>(fileInfo);
            return fileJson?.sha;
        }
    }   
}
