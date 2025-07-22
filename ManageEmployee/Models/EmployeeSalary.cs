using System.ComponentModel.DataAnnotations;

namespace ManageEmployee.Models;
public class EmployeeSalary
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int EmployeeId { get; set; }
    [Required]
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Required]
    public string Title { get; set; }
    [Required]
    public decimal Salary { get; set; }

    public virtual Employee Employee { get; set; }
}
