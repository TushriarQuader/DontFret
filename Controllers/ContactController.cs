using DontFret.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DontFret.Controllers;
/// <summary>
/// This is just for getting the Google Maps API key to the view, so we can use it in the JavaScript to load the map.
/// It's not really a proper controller
/// </summary>
/// <param name="googleMaps"></param>
public class ContactController(IOptions<GoogleMapsSettings> googleMaps) : Controller
{
    // GET: Contact
    public IActionResult Index()
    {
        ViewData["GoogleMapsApiKey"] = googleMaps.Value.ApiKey;
        return View();
    }
}
