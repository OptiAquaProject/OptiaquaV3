using System.Linq;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace OptiAqua.Api.Infraestructura {
    /// <summary>
    /// El proyecto viene de ASP.NET Web API 2, donde un método sin atributo de verbo respondía a
    /// GET por convención. En ASP.NET Core, sin verbo explícito la acción responde a TODOS los
    /// verbos, lo que además rompe la generación de Swagger ("Ambiguous HTTP method for action").
    ///
    /// Esta convención asigna GET por defecto a las acciones que no declaran ningún verbo, dejando
    /// intactas las que ya tienen [HttpGet]/[HttpPost]/[HttpPut]/[HttpDelete]. Así se documenta la
    /// API sin tener que anotar a mano decenas de acciones heredadas, y de paso se cierra el hueco
    /// de que una acción de lectura aceptara también POST/DELETE.
    /// </summary>
    public class VerboGetPorDefectoConvention : IActionModelConvention {
        public void Apply(ActionModel action) {
            foreach (var selector in action.Selectors) {
                bool yaTieneVerbo = selector.ActionConstraints.OfType<HttpMethodActionConstraint>().Any();
                if (!yaTieneVerbo)
                    selector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { "GET" }));
            }
        }
    }
}
