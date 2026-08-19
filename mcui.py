import sys
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QHBoxLayout, QLabel, QLineEdit, QPushButton, 
                             QComboBox, QFrame, QSizePolicy)
from PyQt5.QtCore import Qt

class CoreCraftLauncher(QMainWindow):
    def __init__(self):
        super().__init__()
        
        # Setup Window
        self.setWindowTitle("CoreCraft Launcher")
        self.setFixedSize(1000, 650) # Fixed size to match the provided aspect ratio
        
        # Central Widget and Base Layout
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        main_layout = QVBoxLayout(central_widget)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)
        
        # 1. TOP AREA (Character Panel & Hero/Docs Panel)
        top_area = QWidget()
        top_layout = QHBoxLayout(top_area)
        top_layout.setContentsMargins(10, 10, 10, 0)
        top_layout.setSpacing(10)
        
        # A. Left Panel: Character + Name Input
        left_panel = QFrame()
        left_panel.setObjectName("PanelBorder")
        left_layout = QVBoxLayout(left_panel)
        left_layout.setContentsMargins(15, 15, 15, 15)
        
        # Name Input
        name_label = QLabel("Username")
        name_label.setStyleSheet("font-weight: bold; font-size: 14px;")
        name_input = QLineEdit()
        name_input.setPlaceholderText("Steve")
        name_input.setStyleSheet("padding: 8px; border-radius: 4px; border: 1px solid #333;")
        
        # Character Area (Replace the placeholder with a QLabel(pixmap) for an actual 3D render)
        character_display = QLabel("Character Display\n(Insert 3D Skin Here)")
        character_display.setAlignment(Qt.AlignCenter)
        character_display.setStyleSheet("font-size: 16px; color: #555;")
        
        left_layout.addWidget(name_label)
        left_layout.addWidget(name_input)
        left_layout.addSpacing(10)
        left_layout.addWidget(character_display)
        
        # B. Right Panel: Hero Images and Documentations
        right_panel = QFrame()
        right_panel.setObjectName("PanelBorder")
        right_layout = QVBoxLayout(right_panel)
        right_layout.setContentsMargins(0, 0, 0, 0)
        
        # Documentation / Hero Content Placeholder (Use QStackedWidget for images here)
        hero_docs_area = QLabel("Hero Images / Documentations Area\n(Put your carousel or text docs here)")
        hero_docs_area.setAlignment(Qt.AlignCenter)
        hero_docs_area.setStyleSheet("font-size: 24px; font-weight: bold; color: #888;")
        right_layout.addWidget(hero_docs_area)
        
        # Add Left and Right to Top Layout with Size Ratio (1:3)
        top_layout.addWidget(left_panel, 1)
        top_layout.addWidget(right_panel, 3)
        
        # 2. BOTTOM AREA (Buttons and Logo)
        bottom_area = QWidget()
        bottom_area.setFixedHeight(100) # Give footer some fixed height
        bottom_layout = QHBoxLayout(bottom_area)
        bottom_layout.setContentsMargins(10, 0, 10, 10)
        bottom_layout.setSpacing(15)
        
        # --- Footer Left: 2 Buttons Blocks ---
        footer_btns_layout = QHBoxLayout()
        footer_btns_layout.setSpacing(15)
        
        # 1. "Play with Select Version" Block
        play_block = QFrame()
        play_block.setStyleSheet("QFrame { background-color: black; border-radius: 0px; }")
        play_block_layout = QHBoxLayout(play_block)
        play_block_layout.setContentsMargins(5, 0, 5, 0)
        
        play_button = QPushButton("PLAY")
        play_button.setStyleSheet("QPushButton { background: transparent; color: white; border: none; font-weight: bold; font-size: 20px; }")
        play_button.setFixedSize(100, 60)
        
        version_selector = QComboBox()
        version_selector.addItems(["1.20.4 (Latest)", "1.19.2", "1.16.5", "1.8.9"])
        version_selector.setStyleSheet("QComboBox { background-color: white; color: black; border: none; padding: 5px; font-weight: bold; }")
        version_selector.setFixedSize(120, 40)
        
        play_block_layout.addWidget(play_button)
        play_block_layout.addWidget(version_selector)
        
        # 2. "Mods" Block
        mods_block = QFrame()
        mods_block.setStyleSheet("QFrame { background-color: black; border-radius: 0px; }")
        mods_block_layout = QHBoxLayout(mods_block)
        mods_block_layout.setContentsMargins(0, 0, 0, 0)
        
        mods_button = QPushButton("MODS")
        mods_button.setStyleSheet("QPushButton { background: transparent; color: white; border: none; font-weight: bold; font-size: 20px; }")
        mods_button.setFixedSize(100, 60)
        
        mods_block_layout.addWidget(mods_button)
        
        # Add the two black blocks to footer left
        footer_btns_layout.addWidget(play_block)
        footer_btns_layout.addWidget(mods_block)
        
        # --- Footer Right: Logo & Text ---
        logo_layout = QHBoxLayout()
        logo_layout.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        logo_layout.setSpacing(15)
        
        # Circle Logo
        logo_circle = QLabel()
        logo_circle.setFixedSize(50, 50)
        logo_circle.setStyleSheet("QLabel { background-color: #A4D4E4; border-radius: 25px; }") # Light blue circle
        
        # CoreCraft Text
        brand_text = QLabel("CoreCraft")
        brand_text.setStyleSheet("font-size: 32px; font-weight: bold; font-family: 'Segoe UI';")
        
        logo_layout.addWidget(logo_circle)
        logo_layout.addWidget(brand_text)
        
        # Assemble Bottom Layout (Buttons Left, Spacer, Logo Right)
        bottom_layout.addLayout(footer_btns_layout)
        bottom_layout.addStretch(1) # Spacer to push logo to the right
        bottom_layout.addLayout(logo_layout)
        
        # Add Top and Bottom areas to Main Layout
        main_layout.addWidget(top_area)
        main_layout.addWidget(bottom_area)
        
        # 3. Master Stylesheet (Beige backgrounds and Thick Black Borders)
        self.setStyleSheet("""
            QWidget {
                background-color: #F5F2DE; /* Cream/Beige Background */
            }
            QMainWindow {
                background-color: #FFFFFF;
            }
            QFrame#PanelBorder {
                border: 8px solid black; /* Thick border like the image */
                background-color: #F5F2DE;
            }
            QLineEdit {
                background-color: #FFFFFF;
                font-size: 14px;
                color: black;
            }
        """)
        
        # Final window show
        self.show()

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = CoreCraftLauncher()
    sys.exit(app.exec_())