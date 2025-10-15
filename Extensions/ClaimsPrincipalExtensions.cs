using System.Security.Claims;

namespace backend.Extensions // creamos un namespace para las extensiones
{
    public static class ClaimsPrincipalExtensions // creamos una clase estatica
    {
        // este metodo trae el id del usuario autenticado, si no lo encuentra retorna null
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            if (user == null) // si el user es null
                return null; // retorna null

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier); // busca el claim con el nameidentifier
            if (userIdClaim == null) // si no lo encuentra
                return null; // retorna null

            if (int.TryParse(userIdClaim.Value, out int userId)) // si lo encuentra y lo convierte a int
                return userId; // retorna el id

            return null; // si no lo encuentra retorna null
        }

        // este metodo trae el rol del usuario autenticado
        public static string? GetUserRole(this ClaimsPrincipal user)
        {
            if (user == null) // si el user es null
                return null; // retorna null

            return user.FindFirst(ClaimTypes.Role)?.Value; // busca el claim con el role y lo retorna
        }
    }
}
