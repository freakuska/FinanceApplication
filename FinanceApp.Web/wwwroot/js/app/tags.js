/**
 * Модуль для работы с тегами
 */
class TagsService {
    constructor(apiClient) {
        this.api = apiClient;
    }

    /**
     * Получить все теги
     * @param {string} type - тип тега (Income, Expense, Transfer)
     * @param {string} visibility - видимость (Private, Public)
     * @returns {Promise<Array>}
     */
    async getTags(type = null, visibility = null) {
        try {
            const params = new URLSearchParams();
            if (type) params.append('type', type);
            if (visibility) params.append('visibility', visibility);
            
            const queryString = params.toString();
            const endpoint = queryString ? `/api/tags?${queryString}` : '/api/tags';
            
            return await this.api.get(endpoint);
        } catch (error) {
            console.error('[Tags] getTags failed:', error);
            throw error;
        }
    }

    /**
     * Получить дерево тегов
     * @param {string} type - тип тега (опционально)
     * @returns {Promise<Array>}
     */
    async getTagsTree(type = null) {
        try {
            const params = new URLSearchParams();
            if (type) params.append('type', type);
            
            const queryString = params.toString();
            const endpoint = queryString ? `/api/tags/tree?${queryString}` : '/api/tags/tree';
            
            return await this.api.get(endpoint);
        } catch (error) {
            console.error('[Tags] getTagsTree failed:', error);
            throw error;
        }
    }

    /**
     * Получить тег по ID
     * @param {string} id 
     * @returns {Promise<object>}
     */
    async getTag(id) {
        try {
            return await this.api.get(`/api/tags/${id}`);
        } catch (error) {
            console.error('[Tags] getTag failed:', error);
            throw error;
        }
    }

    /**
     * Создать новый тег
     * @param {object} data - {name, type, parentId, icon, color, visibility}
     * @returns {Promise<object>}
     */
    async createTag(data) {
        try {
            return await this.api.post('/api/tags', {
                name: data.name,
                type: data.type,
                parentId: data.parentId || null,
                icon: data.icon || '',
                color: data.color || '#000000',
                visibility: data.visibility || 'Private'
            });
        } catch (error) {
            console.error('[Tags] createTag failed:', error);
            throw error;
        }
    }

    /**
     * Обновить тег
     * @param {string} id 
     * @param {object} data - {name, icon, color, visibility}
     * @returns {Promise<object>}
     */
    async updateTag(id, data) {
        try {
            return await this.api.put(`/api/tags/${id}`, data);
        } catch (error) {
            console.error('[Tags] updateTag failed:', error);
            throw error;
        }
    }

    /**
     * Удалить тег
     * @param {string} id 
     * @returns {Promise<void>}
     */
    async deleteTag(id) {
        try {
            return await this.api.delete(`/api/tags/${id}`);
        } catch (error) {
            console.error('[Tags] deleteTag failed:', error);
            throw error;
        }
    }

    /**
     * Поиск тегов
     * @param {string} query 
     * @returns {Promise<Array>}
     */
    async searchTags(query) {
        try {
            const params = new URLSearchParams({ query });
            return await this.api.get(`/api/tags/search?${params.toString()}`);
        } catch (error) {
            console.error('[Tags] searchTags failed:', error);
            throw error;
        }
    }

    /**
     * Получить популярные теги
     * @param {number} count 
     * @returns {Promise<Array>}
     */
    async getPopularTags(count = 10) {
        try {
            return await this.api.get(`/api/tags/popular?count=${count}`);
        } catch (error) {
            console.error('[Tags] getPopularTags failed:', error);
            throw error;
        }
    }
}

// Создаем глобальный экземпляр
window.tagsService = new TagsService(window.apiClient);

