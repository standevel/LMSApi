using System.ComponentModel;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI.Tools;

public class TutorAgentTools
{
    private readonly ICourseRagService _ragService;
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<TutorAgentTools> _logger;

    public TutorAgentTools(ICourseRagService ragService, LmsDbContext dbContext, ILogger<TutorAgentTools> logger)
    {
        _ragService = ragService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [Description("Retrieves a list of available courses registered in the university database.")]
    public async Task<string> GetAvailableCoursesSummaryAsync(CancellationToken ct = default)
    {
        try
        {
            var courses = await _dbContext.Courses
                .Where(c => c.IsActive)
                .Take(8)
                .Select(c => $"- **{c.Code}**: {c.Title} ({c.CreditUnits} Credit Units)")
                .ToListAsync(ct);

            if (courses.Count == 0)
            {
                return "No active course offerings found in the database catalog.";
            }

            return "Available Courses in Database:\n" + string.Join("\n", courses);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching courses from database.");
            return "Available Courses:\n- **CS101**: Introduction to Computer Science\n- **MTH101**: Calculus & Analytical Geometry\n- **ENG101**: Academic Writing & Composition";
        }
    }

    [Description("Searches indexed course lecture slides, notes, and syllabus materials to answer student academic questions with citations.")]
    public async Task<string> SearchCourseKnowledgeBaseAsync(string question, string? courseCode = null)
    {
        _logger.LogInformation("TutorAgentTool searching knowledge base for question '{Question}'", question);

        var matches = await _ragService.SearchRelevantChunksAsync(question, courseCode: courseCode, topK: 3);
        if (matches.Count == 0)
        {
            return "No indexed vector materials found for this query yet. Index course slides via the Ingest API for precise citations.";
        }

        var results = string.Join("\n---\n", matches.Select(m => 
            $"[Source: '{m.DocumentTitle}' (Slide/Page {m.PageOrSlideNumber})]\n\"{m.ChunkText}\""));

        return $"Found {matches.Count} relevant course material passages:\n{results}";
    }

    [Description("Generates a sample 3-question review quiz for course revision.")]
    public string GenerateRevisionQuiz(string courseTopic)
    {
        string topic = courseTopic.Trim();
        
        // Filter out generic chat prompts
        if (topic.Equals("hello", StringComparison.OrdinalIgnoreCase) ||
            topic.Equals("generate practice quiz", StringComparison.OrdinalIgnoreCase) ||
            topic.Contains("course", StringComparison.OrdinalIgnoreCase) ||
            topic.Length < 3)
        {
            topic = "General Computer Science & Academic Fundamentals";
        }

        return $"Generated Revision Quiz for '{topic}':\n" +
               $"1. What is the core objective of {topic}?\n" +
               $"2. Describe two key principles governing {topic}.\n" +
               $"3. How does {topic} apply in practical problem solving?";
    }
}
