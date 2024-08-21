using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalWebsite.Context;
using PersonalWebsite.Models;
using Microsoft.Playwright;
using System.Reflection.Metadata;
using static System.Net.WebRequestMethods;
using Azure.Core;
using System.Net.Http;

namespace PersonalWebsite.Repository
{
        
    public interface IProblemRepository
    {
       Task<Boolean> SaveProblem(Problem problem);
        Task<List<Problem>> GetProblems();
       // Task<String?> Scrape(String url);
    }
    public class ProblemRepository : IProblemRepository
    {
        private ProblemContext _context;
        public ProblemRepository(ProblemContext context)
        {
            _context = context;
        }

        public async Task<Boolean> SaveProblem(Problem problem)
        {
            if (problem.Id != 0)
            {
               var currProblem = await _context.Problems.Where((x) => x.Id == problem.Id).FirstOrDefaultAsync();
                if (currProblem == null) return false;
                currProblem.Comments = problem.Comments;
                await _context.SaveChangesAsync();
                return true;
            }
            _context.Problems.Add(problem);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<Problem>> GetProblems()
        {
            var problems  = await _context.Problems.ToListAsync();
            return problems;
        }

    }
}
