<<<<<<< HEAD
using System.ComponentModel.DataAnnotations;

=======
>>>>>>> a5d2af3cb7f84b071e8774aec7f2404d4ac2c1ab
namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; set; }
<<<<<<< HEAD
    
    [Required]
    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}

public class CreateQuoteDto
{
    [Required]
    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;
}
=======

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
>>>>>>> a5d2af3cb7f84b071e8774aec7f2404d4ac2c1ab
