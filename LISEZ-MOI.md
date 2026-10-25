# AbyssX Launcher (application Windows native C# / .NET 8, WPF, sans Electron)

## 1. Obtenir « AbyssX Launcher Setup Installer.exe » (rien à installer sur ton PC)
1. Crée un dépôt sur github.com et envoie-y TOUT le contenu de ce dossier (y compris le dossier caché `.github`).
2. Onglet **Actions** > « Creer l'installateur Windows » > **Run workflow**.
3. Quand c'est vert (3 à 6 min), ouvre l'exécution, en bas **AbyssX-Launcher-Installateur** : télécharge le zip, il contient `AbyssX Launcher Setup Installer.exe`.
Si l'exécution est rouge : ouvre l'étape en erreur et envoie-moi le texte rouge.

## 2. Activer le mode en ligne (comptes réels pour tous les joueurs, gratuit)
1. Crée un projet sur supabase.com (région Europe).
2. SQL Editor > New query > colle `supabase/setup.sql` > Run.
3. Authentication > Providers > Email : laisse « Confirm email » ACTIVÉ (c'est ce qui protège le compte administrateur).
4. Project Settings > API : copie « Project URL » et la clé « anon public » dans `launcher.config.json` (SupabaseUrl, SupabaseKey), puis relance l'étape 1. Cette clé est publique par conception : la sécurité est faite par `setup.sql`.
Sans cette étape, le Launcher fonctionne en **mode local** (comptes et jeux stockés sur le PC, utile pour tester).

## 3. Publier un jeu
Crée un compte avec l'adresse administrateur (`AdminEmail` dans `launcher.config.json` et dans `setup.sql`), confirme l'e-mail, connecte-toi : le menu **Studio** apparaît. Renseigne nom, description, prix, genre, couverture, et le lien de téléchargement (https, par exemple une Release GitHub en .zip ou .exe) puis coche « Publié ».

## Raccourcis
Ctrl+K : recherche. Ctrl+Maj+C : curseur stylé activé/coupé. Réglages : ambiances AbyssX, Blanc doré, Multicolore ; étoiles ; animations.
