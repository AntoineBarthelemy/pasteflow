# Extension PasteFlow

Permet à PasteFlow de lire les **5 onglets consultés le plus récemment**, même cachés derrière un autre onglet, au moment où tu appuies sur son raccourci : leur texte, leurs liens et les champs que tu y as remplis (jamais un mot de passe ni une carte bancaire), pour recopier un formulaire d'un onglet à l'autre. Rien n'est lu en continu, et le texte ne va qu'à l'application PasteFlow, sur ce PC.

## Installer (mode développeur)

1. Lance PasteFlow au moins une fois : il s'inscrit auprès de Chrome et d'Edge.
2. Ouvre `chrome://extensions` (ou `edge://extensions` dans Edge).
3. Active **Mode développeur**.
4. Clique sur **Charger l'extension non empaquetée** et choisis ce dossier `extension`.

L'extension se relie à PasteFlow toute seule, et réessaie chaque minute si PasteFlow n'est pas lancé.

Après une mise à jour de l'extension (nouvelle version du dossier), clique sur l'icône de rechargement de PasteFlow dans `chrome://extensions`.
