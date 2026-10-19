using System.ComponentModel.DataAnnotations;
using Veriqo.Api.Models;

namespace Veriqo.Api.Dtos;

public class ProfileRequest
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    [Required] public string Nationality { get; set; } = string.Empty;
    [Required] public string GovernmentIdNumber { get; set; } = string.Empty;
    [Required] public string Phone { get; set; } = string.Empty;
    [Required] public Address Address { get; set; } = new();
    [Required] public string EmploymentStatus { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    [Range(0, 1000000000)] public decimal AnnualIncome { get; set; }
}
