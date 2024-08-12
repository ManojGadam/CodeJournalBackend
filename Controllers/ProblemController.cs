using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalWebsite.Models;
using PersonalWebsite.Repository;

namespace PersonalWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProblemController : Controller
    {
        private IProblemRepository _problemRepository;
        public ProblemController(IProblemRepository problemRepository)
        {
               _problemRepository = problemRepository;
        }
        [HttpPost]
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
            return Ok(problems);
        }

    }
}
