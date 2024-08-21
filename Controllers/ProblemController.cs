using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using PersonalWebsite.Models;
using PersonalWebsite.Repository;
using System;
using System.Text;

namespace PersonalWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProblemController : Controller
    {
        private readonly HttpClient _httpClient;
        private IProblemRepository _problemRepository;
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

        [HttpGet]
        public async Task<IActionResult> GetProblems()
        {
           var problems = await _problemRepository.GetProblems();
            //fix this
            return Ok(problems);
        }

        [HttpPost("Scrape")]
        public async Task<IActionResult> Scrape([FromBody] String url)
        {
           var leetcodeApiUrl = "https://leetcode.com/graphql";
            int i = 30;
            StringBuilder title = new();
            while (!url[i].Equals('/'))
            {
                title.Append(url[i]);
                i++;
            }
            var query = @"
                query questionTitle($titleSlug: String!) {
                    question(titleSlug: $titleSlug) {
                        questionId
                        questionFrontendId
                        title
                        titleSlug
                        isPaidOnly
                        difficulty
                        likes
                        dislikes
                        categoryTitle
                    }
                }
            ";
            String titleSlug = title.ToString();
            var graphqlRequest = new
            {
                query,
                variables = new { titleSlug } // inferred type
            };

            var httpResponse = await _httpClient.PostAsJsonAsync(leetcodeApiUrl, graphqlRequest);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)httpResponse.StatusCode, "Error fetching data from LeetCode.");
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync();
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
            Problem problem = new Problem() { Name = (string)question["title"], Difficulty = (string)question["difficulty"], Link = url, TitleSlug = (string)question["titleSlug"] };
            await _problemRepository.SaveProblem(problem);
            return Ok(new {isSuccessful = true});
        }

        //[HttpPost("UpdateComment")]
        //public async Task<IActionResult> UpdateComment([FromBody]Problem problem)
        //{
        //    await _problemRepository.UpdateProblem(problem);
        //    return Ok(new { isSuccessful = true });
        //}
    }
}
