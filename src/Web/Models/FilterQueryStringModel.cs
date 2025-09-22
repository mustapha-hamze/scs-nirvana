namespace Web.Models;
public class FilterQueryStringModel
{
    public string location { get; set; }
    public int propertyType { get; set; }
    public string minPrice { get; set; }
    public string maxPrice { get; set; }
    public string bedRoom { get; set; }
    public string yearBuilt { get; set; }
    public string minArea { get; set; }
    public string maxArea { get; set; }
}