# RabbitMQ + MassTransit dans ConnectLive (etat actuel)

## 1. Objectif

Le projet utilise RabbitMQ comme broker de messages et MassTransit comme abstraction .NET pour le pattern publish/subscribe.

Objectifs techniques :
- decoupler les services (API principale, Notification API, etc.)
- traiter des evenements de maniere asynchrone
- eviter le couplage direct entre producteur et consommateur

---

## 2. Services concernes

### API principale (ConnectLive.Core.Api)
- Fichier bootstrap: ConnectLive.Core/ConnectLive.Core/Program.cs
- Extension bus: ConnectLive.Core/ConnectLive.Core/Extensions/HostExtensions.cs

Role cible:
- publier des evenements d'integration

Etat actuel:
- l'enregistrement MassTransit est present mais commente dans Program.cs
- ligne commentee: AddBusPublisherRegistration(...)

### Service consommateur (ConnectLive.Notification.Api)
- Fichier bootstrap: ConnectLive.Core/ConnectLive.Notification.Api/Program.cs
- Extension host locale: ConnectLive.Core/ConnectLive.Notification.Api/Extensions/MyCustomWebHosting.cs
- Extension de bus consommatrice: Connectlive.WebHosting/WebHostExtensions.cs

Role:
- declarer le consumer
- ecouter une queue RabbitMQ
- executer la logique metier sur message recu

Etat actuel:
- actif (AddBusRegistration est appele dans Program.cs)

---

## 3. Contrat de message

Fichier: ConnectLive.Core/ConnectLive.Domain/Contracts/UserCreatedContractQuery.cs

Le contrat transporte dans RabbitMQ:
- Email
- FullName

Principe important:
- en event-driven, le contrat est la frontiere entre services
- toute evolution de contrat doit etre versionnee avec prudence (compatibilite)

---

## 4. Consumer

Fichier: ConnectLive.Core/ConnectLive.Application/BusEvents/UserCreatedConsumer.cs

Le consumer implemente:
- IConsumer<UserCreatedContractQuery>

Comportement actuel:
- reception du message
- log d'information avec les donnees du contrat

Ce consumer est bien cable pour une logique reelle future (notifications, workflows, etc.).

---

## 5. Configuration MassTransit / RabbitMQ

## 5.1 Configuration commune du host RabbitMQ

Dans:
- ConnectLive.Core/ConnectLive.Core/Extensions/HostExtensions.cs
- ConnectLive.Core/ConnectLive.Notification.Api/Extensions/MyCustomWebHosting.cs
- Connectlive.WebHosting/WebHostExtensions.cs

Les memes cles de configuration sont lues:
- EventBusConnection
- EventBusUserName
- EventBusPassword

La methode Configure(...) utilise Bus.Factory.CreateUsingRabbitMq(...) puis cfg.Host(...).

## 5.2 Variables d'environnement Docker

Dans ConnectLive.Core/docker-compose.override.yml:
- EventBusConnection=rabbitmq://rabbitmq:5672
- EventBusUsername=user
- EventBusPassword=password

Dans le meme fichier:
- RABBITMQ_DEFAULT_USER=user
- RABBITMQ_DEFAULT_PASS=password

Note importante:
- le conteneur RabbitMQ expose l'UI management sur le port 15672 (voir docker-compose.yml)

---

## 6. Topologie de messagerie (etat courant)

Dans Connectlive.WebHosting/WebHostExtensions.cs:
- AddConsumer<UserCreatedConsumer>()
- ReceiveEndpoint("users.service", ...)

Consequence:
- une queue nommee users.service est ecoutee
- les messages compatibles UserCreatedContractQuery sont dispatches au UserCreatedConsumer

Schema logique:

Core API (publisher) -> exchange MassTransit/RabbitMQ -> queue users.service -> UserCreatedConsumer (Notification API)

---

## 7. Ce qui fonctionne aujourd'hui vs ce qui est en attente

Fonctionne:
- infrastructure RabbitMQ dans Docker Compose
- service Notification avec consumer en ecoute
- contrat de message + consumer

En attente / partiellement active:
- publication d'evenements depuis l'API principale: non active dans Program.cs (ligne commentee)
- cas d'usage de publish dans handlers/commands: pas de publish actif dans le flux metier actuel

Conclusion pratique:
- le cote "consume" est pret
- le cote "publish metier" reste a activer pour un flux de bout en bout

---

## 8. Sequence d'execution de bout en bout (quand publish est active)

1. Une action metier cree un utilisateur.
2. Le service producteur publie UserCreatedContractQuery via IPublishEndpoint.
3. MassTransit serialise le message et l'envoie a RabbitMQ.
4. RabbitMQ route le message vers la topologie associee (exchange -> queue users.service).
5. MassTransit cote Notification recoit le message depuis users.service.
6. UserCreatedConsumer.Consume(...) est execute.

Impact architecture:
- le producteur ne connait pas le service consommateur
- le consommateur peut evoluer sans changer le code producteur

---

## 9. Demarrage local

Depuis ConnectLive.Core:
- docker compose -f docker-compose.yml -f docker-compose.override.yml up --build

Puis lancer les APIs:
- ConnectLive.Core.Api
- ConnectLive.Notification.Api
- (optionnel) ConnectLive.Newsletter.Api

UI RabbitMQ:
- http://localhost:15672
- user/password: user/password (compose override)

---

## 10. Checklist diagnostic

Si aucun message n'est consomme:
- verifier que Notification API demarre sans erreur de connexion bus
- verifier EventBusConnection et credentials
- verifier queue users.service dans l'UI RabbitMQ
- verifier que le publish est reellement appele dans le code metier
- verifier les logs MassTransit et logs consumer

---

## 11. Pistes d'amelioration recommandees

1. Activer explicitement le publish metier dans ConnectLive.Core.Api et ajouter un endpoint/test de verification.
2. Ajouter des retries MassTransit (policy) et dead-letter strategy.
3. Centraliser la configuration bus dans une seule librairie pour eviter la duplication.
4. Uniformiser les packages MassTransit (eviter le melange v8.1.3 et v7.3.1).
5. Ajouter tests d'integration event-driven (publisher -> broker -> consumer).
