# ReadMe

ReadMe est une application .NET MAUI multiplateforme qui permet de parcourir une bibliothèque de livres, consulter la fiche détaillée d’un ouvrage, lire un EPUB et ajouter de nouveaux livres via un fichier EPUB. L’application s’appuie sur une API Node.js/Express locale pour charger la liste des livres, récupérer les détails d’un livre et gérer l’envoi ou la suppression d’un ouvrage.

## Fonctionnalités principales

- Affichage paginé de la bibliothèque depuis la page d’accueil.
- Ouverture d’une fiche livre avec métadonnées et accès à la lecture.
- Lecture d’un livre EPUB dans le lecteur intégré.
- Ajout d’un livre via un formulaire avec sélection de fichier EPUB.
- Suppression d’un livre depuis sa fiche détail.

## Structure du projet

- ReadMe/ : application MAUI.
- api/ : serveur Express utilisé par l’application.
- Doc/ : documents de travail.

## Prérequis

- .NET 8 SDK avec le workload .NET MAUI installé.
- Node.js 18+.
- Un émulateur ou un appareil compatible pour lancer l’application mobile/desktop.

## Lancer l’API

```bash
cd api
npm install
npm start
```

L’API écoute par défaut sur http://localhost:3000.

## Lancer l’application MAUI

Ouvrir la solution ReadMe.sln dans Visual Studio, puis lancer le projet ReadMe sur la cible souhaitée. L’application utilise par défaut l’API locale http://localhost:3000/.

Si l’API est hébergée ailleurs, la base URL peut être ajustée via les paramètres enregistrés par l’application.

## Tests fonctionnels

### Test : Ajout d’un nouveau livre EPUB

Pré-requis : L’application est installée et l’API est démarrée.

Étapes :
1. Depuis l’écran d’accueil, ouvrir l’écran d’ajout d’un livre.
2. Saisir le titre, l’auteur et les champs obligatoires.
3. Sélectionner un fichier EPUB valide.
4. Valider l’envoi.

Résultat attendu : Le livre est envoyé à l’API, un message de succès s’affiche et le nouveau livre devient visible dans la liste après rafraîchissement.

### Test : Consultation de la fiche d’un livre et ouverture du lecteur

Pré-requis : Au moins un livre est présent dans la liste.

Étapes :
1. Depuis la page d’accueil, sélectionner un livre.
2. Vérifier l’affichage de la fiche détaillée avec les métadonnées.
3. Cliquer sur le bouton de lecture.
4. Contrôler l’ouverture du lecteur EPUB.

Résultat attendu : La fiche du livre s’ouvre correctement, puis le lecteur affiche le contenu du livre sélectionné.

### Test : Suppression d’un livre depuis la fiche détail

Pré-requis : Un livre existe dans la liste.

Étapes :
1. Ouvrir la fiche d’un livre depuis la liste.
2. Cliquer sur le bouton de suppression.
3. Confirmer la suppression dans la boîte de dialogue.
4. Revenir à la liste des livres.

Résultat attendu : Le livre est supprimé côté API et n’apparaît plus dans la liste principale après retour à l’accueil.

## Notes techniques

- L’API expose les routes principales pour la liste, le détail, l’upload et la suppression des livres.
- Les fichiers EPUB sont stockés localement dans api/storage.
- L’application utilise l’injection de dépendances pour accéder au client API.