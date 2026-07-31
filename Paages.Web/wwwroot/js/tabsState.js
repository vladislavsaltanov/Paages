const COOKIE_NAME = 'paages_tabs';
const COOKIE_DAYS = 30;

export function loadTabs(){
    const match = document.cookie.match(new RegExp('(^| )' + COOKIE_NAME + '=([^;]+)'));
    if (!match) return null;

    try{
        return JSON.parse(decodeURIComponent(match[2]));
    }catch{
        return null;
    }
}

export function saveTabs(tabs, activeTab){
    const value = encodeURIComponent(JSON.stringify({tabs, activeTab}));
    const expires = new Date(Date.now() + COOKIE_DAYS * 864e5).toUTCString();
    document.cookie = `${COOKIE_NAME}=${value}; expires=${expires}; path=/; SameSite=Lax`;
}