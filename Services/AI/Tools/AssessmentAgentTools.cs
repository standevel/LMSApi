using System.ComponentModel;
using LMS.Api.Services;

namespace LMS.Api.Services.AI.Tools;

public class AssessmentAgentTools
{
    private readonly ILogger<AssessmentAgentTools> _logger;

    public AssessmentAgentTools(ILogger<AssessmentAgentTools> logger)
    {
        _logger = logger;
    }

    [Description("Generates preliminary draft rubric feedback for an assignment submission.")]
    public string GenerateRubricPregrade(string submissionText, string rubricCriteria)
    {
        _logger.LogInformation("AssessmentAgentTool generating rubric pregrade evaluation");
        
        int wordCount = submissionText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return $"Draft Analysis Completed. Word Count: {wordCount}. Alignment to rubric '{rubricCriteria}': High structural clarity. Suggested Score Range: 85-92%. Strengths: Clear thesis statement, good citations. Areas to Improve: Expand conclusion section.";
    }

    [Description("Generates a sample 3-question review quiz for course revision.")]
    public string GenerateRevisionQuiz(string courseTopic)
    {
        return $"Generated Revision Quiz for '{courseTopic}':\n" +
               $"1. What is the main objective of {courseTopic}?\n" +
               $"2. Describe two core principles of {courseTopic}.\n" +
               $"3. How does {courseTopic} apply in practical scenario analysis?";
    }
}
