using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MovieFlix.Api.Routing
{
    /// <summary>Route constraint accepting only the public media type names.</summary>
    public class MediaTypeRouteConstraint : IRouteConstraint
    {
        public bool Match(
            HttpContext? httpContext,
            IRouter? route,
            string routeKey,
            RouteValueDictionary values,
            RouteDirection routeDirection)
        {
            if (!values.TryGetValue(routeKey, out var value))
            {
                return false;
            }

            var text = Convert.ToString(value);

            return string.Equals(text, "movie", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "tv", StringComparison.OrdinalIgnoreCase);
        }
    }
}
