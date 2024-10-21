using Socket.Newtonsoft.Json.Linq;

namespace SmartStorage
{
    public static class Utils
    {
        public static bool HasDataAndStateBelow100(string data)
        {
            // Vérifie si la propriété "data" n'est pas vide
            if (!string.IsNullOrEmpty(data))
            {
                // Parse le JSON de "data"
                var jsonData = JObject.Parse(data);

                // Vérifie si "statePercentage" existe et s'il est inférieur à 100.0
                if (jsonData["statePercentage"] != null &&
                    jsonData["statePercentage"].Value<double>() < 100.0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasDataAndAmmoNotZero(string data)
        {
            // Vérifie si la propriété "data" n'est pas vide
            if (!string.IsNullOrEmpty(data))
            {
                // Parse le JSON de "data"
                var jsonData = JObject.Parse(data);

                // Vérifie si "currentAmmo" existe et s'il est différent de 0
                if (jsonData["currentAmmo"] != null &&
                    jsonData["currentAmmo"].Value<int>() != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
