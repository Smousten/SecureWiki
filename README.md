# Speciale

## System
This installation guide is for Ubuntu 20.04. The system should also work on other Linux distributions but this has not been tested.

## Overview of System
This is the implementation of a system for secure storage and revision of files in a cloud-based environment.
The system uses cryptographic access control to provide end-to-end encryption such that users do not have to depend on the trustworthiness and security of the hosting server. 

The system uses MediaWiki for its remote storage server and users can interact with files through a mounted directory provided by FUSE. 

The code for the file system application is found in the FUSE directory.  
The code for the Client Application is found in the SecureWiki directory. 

The Client Application main project is in SecureWiki/Avalonia.NETCoreMVVMApp.  
A test project with unit tests and integration tests is located in SecureWiki/SecureWikiTests.


## Dependencies

### Installation of FUSE
Fresh install:
get libfuse3-dev
install fuse3



### Setup the FUSE Application
Make sure that the fuse directory contains a folder named *directories*, which contains two empty folders named *mountdir* and *rootdir*.

### Installation of MediaWiki Server \*
Follow the steps below. Visit https://www.mediawiki.org/wiki/Manual:Running_MediaWiki_on_Debian_or_Ubuntu for more information. 

\* Not necessary if you connect to remote server

#### Ensure that your system is up to date
`sudo apt update`  
`sudo apt upgrade`

#### Install Apache, PHP, and MySQL
`sudo apt-get install apache2 mariadb-server php php-mysql libapache2-mod-php php-xml php-mbstring`

#### Get MediaWiki
Download MediaWiki  
`cd /tmp/
wget https://releases.wikimedia.org/mediawiki/1.36/mediawiki-1.36.1.tar.gz`

Extract in your Web directory  
`tar -xvzf /tmp/mediawiki-*.tar.gz`  
`sudo mkdir /var/lib/mediawiki`  
`sudo mv mediawiki-*/* /var/lib/mediawiki`


#### Configuring MySQL
* Create new mysql user  
`# sudo mysql -u root -p 
Enter password: Enter password of mysql root user (if you have not configured password it will be blank, so just press enter)`  
`mysql> CREATE USER 'new_mysql_user'@'localhost' IDENTIFIED BY 'THISpasswordSHOULDbeCHANGED';`  
`mysql> quit;`  

* Create a new mysql database  
`# sudo mysql -u root -p  
mysql> CREATE DATABASE my_wiki;  
mysql> use my_wiki;  
Database changed`  

* Grant the new mysql user access to the new mysql database  
`mysql> GRANT ALL ON my_wiki.* TO 'new_mysql_user'@'localhost';  
Query OK, 0 rows affected (0.01 sec)  
mysql> quit;`  

#### Configure MediaWiki  
Navigate your browser to http://localhost/mediawiki

If this page does not exist, then use the following command  
`sudo ln -s /var/lib/mediawiki /var/www/html/mediawiki`

Fill out all the fields in the configuration form and press the continue button. 
Use the username and password of the mysql user previously created when needed.

During the configuration process you will have to download *LocalSettings.php* that must be saved to the parent directory of the neww wiki.  
`sudo mv ~/Downloads/LocalSettings.php /var/lib/mediawiki/`

Navigate to  http://localhost/mediawiki to see your new wiki.

#### Configure LocalSettings.php
Edit the file  
`gksudo gedit /var/lib/mediawiki/LocalSettings.php`

Add the following lines to configure your wiki to our system  

* Allow first letter of page titles to be non-capital  
`$wgCapitalLinks = false;`

* Allow more title characters  
`$wgLegalTitleChars = " %!\"$&'()*,\\-.\\/0-9:;=?@A-Z\\\\^_\``a-z~\\x80-\\xFF+";`

* Set the max article size  
`$wgMaxArticleSize = 204800000;`

* Set the max upload size  
`$wgMaxUploadSize = 2147483647;`




