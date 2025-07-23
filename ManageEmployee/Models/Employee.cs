using System.ComponentModel.DataAnnotations;

namespace ManageEmployee.Models;
public class Employee
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string SSN { get; set; }
    [Required]
    public DateTime DOB { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Zip { get; set; }
    public string Phone { get; set; }
    public DateTime JoinDate { get; set; }
    public DateTime? ExitDate { get; set; }

    //public virtual ICollection<EmployeeSalary> Salaries { get; set; }
}
