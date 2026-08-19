from pathlib import Path
import minecraft_launcher_lib as mc
import subprocess
import math

minecraft_dir = Path(Path.home() / ".Delta Minecraft")
minecraft_dir.mkdir(parents=True, exist_ok=True)

print("CoreCraft Client")
choice = input("1. Play\n2. Install\n3. available Versions\n4. installed versions\n5. Exit\n6. Quiz \nChoose: ")

def run_minecraft(username):
    options = mc.utils.generate_test_options()
    options["username"] = username
    minecraft_command = mc.command.get_minecraft_command("1.17", minecraft_dir, options)
    subprocess.run(minecraft_command)



def install_minecraft():
    mc.install.install_minecraft_version("1.17", minecraft_dir)
    print("Minecraft installed successfully")
    print("now rerun program and select play to play minecraft")

def listversions():
    print("available versions:")
    available_versions = mc.utils.get_version_list()
    for version in available_versions:
        print(version["id"])  

def installedversion():  
    installed_versions = mc.utils.get_installed_versions(minecraft_dir)
    if installed_versions:
        print("Installed versions:")
        for v in installed_versions:
            print(v["id"])
    else:
        print("you got no version vro install one")

def quiz():
    print("quiz time hmm")
    print("--------")
    print("what is 2 + 2?")
    answer = input("Answer: ")
    if answer == "4":
        print("correct answer wow")
    else:
        print("wrong answer you cooked")

    print("--------")
    print("who made minecraft?")
    answer = input("Answer: ")
    if answer == "Notch," or answer == "notch":
        print("correct answer wow")
    else:
        print("wrong answer you cooked")

    print("--------")
    print("when minecraft launched?")
    answer = input("Answer: ")
    if answer == "2011":
        print("correct answer wow")
    else:
        print("wrong answer you cooked")

if choice == "1":
    print("launching minecraft...")
    print("Please enter your minecraft username :) you wanna use:")
    username = input("Username: ")
    run_minecraft(username)

elif choice == "2":
    print("installing 1.17 minecraft (cracked )...")
    install_minecraft()

elif choice == "3":
    print("available versions")
    listversions()

elif choice == "4":
    print("installed versions")
    installedversion()

elif choice == "5":
    print("no play ok, bye")
    exit()

elif choice == "6":
    quiz()

else:
    print("huh choose a valid option next time vro")