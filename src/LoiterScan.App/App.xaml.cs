using System.Windows;

namespace LoiterScan.App;

public partial class App : Application
{
    // Composition root. Register here via Microsoft.Extensions.DependencyInjection:
    //   IPropagator   -> Sgp4Propagator           (swap to AstroStds XP later)
    //   ICatalogSource-> CelesTrakCatalogSource    (swap to Space-Track later)
    //   LoiterScanDbContext, DetectionPipeline, analytics, and the view-models.
}
