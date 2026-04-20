using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlindMatchPAS.ViewModels.Student
{
    public class SubmitProposalViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 10)]
        [Display(Name = "Project Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Abstract is required.")]
        [StringLength(2000, MinimumLength = 100)]
        [Display(Name = "Project Abstract")]
        [DataType(DataType.MultilineText)]
        public string Abstract { get; set; } = string.Empty;

        [Required(ErrorMessage = "Technical stack is required.")]
        [StringLength(500)]
        [RegularExpression(@"^[A-Za-z][A-Za-z0-9 .#+,/()-]{0,499}$",
            ErrorMessage = "Technical stack must start with a letter and may only contain letters, digits, spaces and common symbols (e.g. ASP.NET Core, Python 3, C++).")]
        [Display(Name = "Technical Stack")]
        public string TechnicalStack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a research area.")]
        [Display(Name = "Research Area")]
        public int ResearchAreaId { get; set; }

        public IEnumerable<SelectListItem> ResearchAreas { get; set; } = new List<SelectListItem>();
    }
}
