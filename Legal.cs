using System;
using System.Windows;
using System.Windows.Controls;

namespace AbyssXLauncher
{
    // Modèles de textes à relire et adapter (ils ne remplacent pas l'avis d'un juriste).
    public static class Legal
    {
        public static readonly Tuple<string, string>[] Tabs =
        {
            Tuple.Create("cgu", "Conditions d'utilisation"),
            Tuple.Create("privacy", "Confidentialité"),
            Tuple.Create("mentions", "Mentions légales"),
            Tuple.Create("licence", "Licence")
        };

        const string Maj = "24 octobre 2026";

        public static string Text(string id, Config c)
        {
            string mail = c.ContactEmail;
            switch (id)
            {
                case "privacy":
                    return string.Join("\n", new[]
                    {
                        "# Politique de confidentialité",
                        "Version 1.0, mise à jour le " + Maj + ".",
                        "## Responsable du traitement",
                        "AbyssX, éditeur non professionnel. Contact : " + mail + ".",
                        "## Données traitées",
                        "Compte : adresse e-mail, identifiant, mot de passe (jamais lisible : il est haché ou géré par le service d'authentification), photo de profil facultative.",
                        "Utilisation : jeux ajoutés à ta bibliothèque, jeux installés, temps de jeu et réglages, enregistrés sur ton ordinateur.",
                        "## Où sont stockées tes données",
                        "Mode en ligne : ton compte, ta photo de profil et le catalogue sont hébergés chez notre prestataire Supabase, qui agit comme sous-traitant au sens du RGPD. Ta bibliothèque, tes jeux installés et tes réglages restent sur ton ordinateur.",
                        "Mode local : tout reste sur ton ordinateur.",
                        "Aucune publicité, aucun traceur, aucune mesure d'audience, aucune revente de données. Le Launcher n'utilise pas de cookies.",
                        "## Pourquoi et sur quelle base",
                        "Créer et gérer ton compte et fournir les fonctions du Launcher (exécution du service, art. 6.1.b du RGPD). Sécuriser les accès (intérêt légitime, art. 6.1.f).",
                        "## Durée de conservation",
                        "Tant que ton compte existe. Tu peux en demander la suppression à tout moment.",
                        "## Tes droits",
                        "Accès, rectification, effacement, portabilité, limitation et opposition : écris à " + mail + ". Tu peux aussi saisir la CNIL (cnil.fr).",
                        "## Mineurs",
                        "Le Launcher est destiné aux personnes de 15 ans ou plus, ou avec l'accord d'un représentant légal."
                    });
                case "mentions":
                    return string.Join("\n", new[]
                    {
                        "# Mentions légales",
                        "Mise à jour le " + Maj + ".",
                        "## Éditeur",
                        "AbyssX, éditeur non professionnel (personne physique). Contact : " + mail + ".",
                        "## Hébergement",
                        "Le Launcher est un logiciel installé sur l'ordinateur de l'utilisateur. En mode en ligne, le compte et le catalogue sont hébergés par Supabase, Inc. (supabase.com).",
                        "## Propriété intellectuelle",
                        "Le Launcher, le nom « AbyssX » et le logo sont © 2026 AbyssX. Les jeux appartiennent à leurs auteurs respectifs.",
                        "## Signaler un contenu",
                        "Pour signaler un contenu illicite ou une atteinte à tes droits, écris à " + mail + " : il sera traité rapidement.",
                        "## Droit applicable",
                        "Droit français."
                    });
                case "licence":
                    return string.Join("\n", new[]
                    {
                        "# Licence d'utilisation",
                        "AbyssX Launcher 1.0.0. Copyright © 2026 AbyssX. Tous droits réservés.",
                        "## Droits accordés",
                        "Une licence personnelle, non exclusive, non transférable et révocable pour installer et utiliser gratuitement le Launcher sur tes appareils.",
                        "## Interdictions",
                        "Revendre, louer ou sous-licencier le logiciel ; le modifier ou le décompiler, sauf dans les limites imposées par la loi ; retirer les mentions de droits d'auteur.",
                        "## Composants tiers",
                        "Le Launcher repose sur .NET et Windows Presentation Foundation (licence MIT, Microsoft). Leurs textes de licence sont disponibles sur dotnet.microsoft.com.",
                        "## Jeux",
                        "Chaque jeu peut avoir ses propres conditions, indiquées par son auteur.",
                        "## Garantie",
                        "Le logiciel est fourni « en l'état », sans garantie, dans les limites autorisées par la loi."
                    });
                default:
                    return string.Join("\n", new[]
                    {
                        "# Conditions d'utilisation",
                        "Version 1.0, mise à jour le " + Maj + ".",
                        "## 1. Objet",
                        "AbyssX Launcher (« le Launcher ») est un logiciel gratuit pour découvrir, obtenir, installer et lancer des jeux publiés par AbyssX (« l'Éditeur »). En créant un compte ou en utilisant le Launcher, tu acceptes ces conditions.",
                        "## 2. Compte",
                        "Un compte (e-mail, identifiant, mot de passe) est nécessaire. Les informations fournies doivent être exactes. Tu dois avoir 15 ans ou plus, ou l'accord de ton représentant légal. Garde ton mot de passe secret : l'Éditeur ne te le demandera jamais.",
                        "## 3. Jeux et prix",
                        "Seul l'administrateur publie des jeux dans le catalogue officiel. Les prix éventuels sont indiqués sur la page de chaque jeu. Les jeux restent la propriété de leurs auteurs : tu obtiens un droit d'usage personnel et non commercial.",
                        "## 4. Comportements interdits",
                        "Contourner la sécurité du Launcher, accéder au compte d'une autre personne, extraire ou redistribuer les fichiers des jeux, usurper une identité, diffuser un logiciel malveillant ou mener toute activité illégale.",
                        "## 5. Suspension et suppression",
                        "L'Éditeur peut suspendre un compte qui ne respecte pas ces conditions. Tu peux demander la suppression de ton compte à tout moment par e-mail.",
                        "## 6. Disponibilité et responsabilité",
                        "Le Launcher est fourni « en l'état », sans garantie d'absence d'erreur. Dans les limites permises par la loi, l'Éditeur n'est pas responsable des dommages indirects ni de la perte de données locales. Tes droits impératifs de consommateur ne sont pas affectés.",
                        "## 7. Données personnelles",
                        "Voir la Politique de confidentialité.",
                        "## 8. Évolution des conditions",
                        "Ces conditions peuvent évoluer. La nouvelle version s'applique dès sa publication dans le Launcher.",
                        "## 9. Droit applicable",
                        "Droit français. En cas de difficulté, écris d'abord à " + mail + " pour trouver une solution amiable."
                    });
            }
        }

        public static UIElement Doc(string id, Config c)
        {
            var sp = new StackPanel { MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var raw in Text(id, c).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("# ")) sp.Children.Add(Pad(Ui.Txt(line.Substring(2), 24, true, null, true), 0, 0, 0, 4));
                else if (line.StartsWith("## ")) sp.Children.Add(Pad(Ui.Txt(line.Substring(3), 16, true, null, true), 0, 18, 0, 4));
                else sp.Children.Add(Pad(Ui.Txt(line, 13.5, false, Ui.T.Muted, true), 0, 0, 0, 6));
            }
            return sp;
        }

        static TextBlock Pad(TextBlock t, double l, double top, double r, double b)
        {
            t.Margin = new Thickness(l, top, r, b);
            return t;
        }
    }
}
