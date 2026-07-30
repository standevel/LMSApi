namespace LMS.Api.Services.AI.Models;

public enum AgentPersona
{
    Advisor,
    Tutor,
    InstructorTA,
    Bursar,
    Admission,
    AdminAssistant,
    GeneralAssistant
}

public class AgentChatRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public AgentPersona Persona { get; set; } = AgentPersona.Advisor;
    public string? StudentId { get; set; }
    public string? CourseId { get; set; }
}

public class AgentChatResponse
{
    public string ResponseText { get; set; } = string.Empty;
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();
    public AgentPersona Persona { get; set; }
    public List<string> ToolsExecuted { get; set; } = new();
    public GenerativeCardDto? Card { get; set; }
}

public class GenerativeCardDto
{
    public string CardType { get; set; } = "info"; // info, gpa_projection, fee_clearance, rubric_pregrade, course_recommendation
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public List<CardActionDto> Actions { get; set; } = new();
}

public class CardActionDto
{
    public string Label { get; set; } = string.Empty;
    public string ActionType { get; set; } = "navigate"; // navigate, execute_api, prompt
    public string Target { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}
