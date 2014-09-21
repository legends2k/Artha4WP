##Artha for Windows Phone##

A handy, WordNet-based thesaurus for Windows Phone written in C#. The application is a solution with two projects:

* WordNet - library that reads [Princeton's WordNet](http://wordnet.princeton.edu/) database files and provide them as Senses
* Artha4WP - a Windows Phone app. written against the Silverlight for Windows Phone runtimes

##Notes##
WordNet dictionary files, namely

* index.noun
* index.verb
* index.adj
* index.adv
* data.noun
* data.verb
* data.adj
* data.adv

are not checked-in as part of the project. Make sure they present under `/Artha4WP/dict`. The latest WordNet database files can be found [here](http://wordnet.princeton.edu/wordnet/download/current-version/).
